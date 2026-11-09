using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;
using MMS.Play.Contracts.Models.Notifications;
using MMS.Play.Infrastructure.Lessons;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;
using MMS.Play.Infrastructure.Telegram;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MMS.Play.Infrastructure.Notifications;

/// <summary>
/// Payment reminders: one message per student who owes (see <see cref="DueLessons"/>) to the student and one to each
/// parent, each recipient with their own "Reminders" checkbox on. A parent is never reminded as a student.
/// At the daily payment reminder time a person is reminded about a student again only after "every N days";
/// <see cref="PaymentReminderDelivery"/> rows remember who got what, the "Remind" button on the website included.
/// </summary>
public partial class PaymentReminderService(
    ApplicationDbContext context,
    ITelegramBotClient botClient,
    IMessageTemplateService templateService,
    INotificationSettingsService settingsService,
    SchoolClock clock,
    IOptions<LessonReminderSettings> reminderOptions,
    IOptions<PaymentReminderSettings> paymentOptions,
    ILogger<PaymentReminderService> logger) : IPaymentReminderService
{
    // a reminder sent a little earlier than exactly N days ago (the job runs every minute) still counts as N days ago
    private static readonly TimeSpan RepeatSlack = TimeSpan.FromHours(1);

    public async Task<LessonReminderResult> SendDueAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        if (!settings.PaymentRemindersEnabled)
            return new LessonReminderResult(0, 0, 0);

        var now = clock.Now;
        var slot = DateOnly.FromDateTime(now).ToDateTime(settings.PaymentReminderTime);
        if (now < slot || now - slot > TimeSpan.FromMinutes(reminderOptions.Value.MissedSlotGraceMinutes))
            return new LessonReminderResult(0, 0, 0);

        var due = await DueLessons.FindAsync(context, null, now, cancellationToken);
        if (due.Count == 0)
            return new LessonReminderResult(0, 0, 0);

        var studentIds = due.Select(lesson => lesson.StudentId).Distinct().ToList();
        var cutoff = clock.UtcNow - TimeSpan.FromDays(settings.PaymentReminderEveryDays) + RepeatSlack;
        var recent = (await context.PaymentReminderDeliveries
                .AsNoTracking()
                .Where(delivery => studentIds.Contains(delivery.StudentId) && delivery.SentAtUtc > cutoff)
                .Select(delivery => new { delivery.SubscriberId, delivery.StudentId })
                .ToListAsync(cancellationToken))
            .Select(x => (x.SubscriberId, x.StudentId))
            .ToHashSet();

        var recipients = (await FindRecipientsAsync(studentIds, cancellationToken))
            .Where(recipient => !recent.Contains((recipient.SubscriberId, recipient.StudentId)))
            .ToList();

        var result = await SendAsync(recipients, due, isManual: false, cancellationToken);
        if (recipients.Count > 0)
            LogPaymentRemindersSent(logger, result.Sent, result.Failed, result.Unsubscribed);
        return result;
    }

    public async Task<LessonReminderResult> RemindStudentAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var due = await DueLessons.FindAsync(context, [studentId], clock.Now, cancellationToken);
        if (due.Count == 0)
            throw new ValidationFailedException("StudentId", "The student owes nothing: there are no unpaid lessons that took place.");

        var recipients = await FindRecipientsAsync([studentId], cancellationToken);
        if (recipients.Count == 0)
            throw new ValidationFailedException("StudentId", await ExplainNobodyAsync(studentId, cancellationToken));

        var result = await SendAsync(recipients, due, isManual: true, cancellationToken);
        if (result.Sent == 0)
            throw new ValidationFailedException("StudentId", "Telegram did not take the messages, try again later.");
        return result;
    }

    public async Task<PaymentRemindAllResult> RemindAllAsync(CancellationToken cancellationToken = default)
    {
        var due = await DueLessons.FindAsync(context, null, clock.Now, cancellationToken);
        if (due.Count == 0)
            throw new ValidationFailedException("Students", "Nobody owes: there are no unpaid lessons that took place.");

        var studentIds = due.Select(lesson => lesson.StudentId).Distinct().ToList();
        var recipients = await FindRecipientsAsync(studentIds, cancellationToken);
        var reached = recipients.Select(recipient => recipient.StudentId).Distinct().Count();

        var result = await SendAsync(recipients, due, isManual: true, cancellationToken);
        LogPaymentRemindersSent(logger, result.Sent, result.Failed, result.Unsubscribed);

        return new PaymentRemindAllResult(studentIds.Count, studentIds.Count - reached, result.Sent, result.Failed, result.Unsubscribed);
    }

    /// <summary>
    /// Why neither the student nor a parent can get a reminder, person by person, so the admin knows what to change.
    /// </summary>
    private async Task<string> ExplainNobodyAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var student = await context.TelegramSubscribers
            .AsNoTracking()
            .Where(subscriber => subscriber.Id == studentId)
            .Select(subscriber => new
            {
                subscriber.FirstName,
                subscriber.IsParent,
                subscriber.IsActive,
                subscriber.RemindersEnabled,
                Parents = subscriber.Parents
                    .Select(parent => new { parent.FirstName, parent.IsParent, parent.IsActive, parent.RemindersEnabled })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (student is null)
            return "The student does not exist.";

        var reasons = new List<string>
        {
            student switch
            {
                { IsParent: true } => $"{student.FirstName} is marked as a parent, and parents get reminders about their children only, not as students",
                { IsActive: false } => $"{student.FirstName} is not subscribed in the bot (pressed Stop or never pressed Start)",
                _ => $"{student.FirstName} has «Reminders» off on the Telegram page"
            }
        };

        if (student.Parents.Count == 0)
            reasons.Add("no parents are linked to the student");

        reasons.AddRange(student.Parents.Select(parent => parent switch
        {
            { IsParent: false } => $"{parent.FirstName} is linked as a parent but the «Parent of» checkbox is off",
            { IsActive: false } => $"parent {parent.FirstName} is not subscribed in the bot",
            _ => $"parent {parent.FirstName} has «Reminders» off"
        }));

        return $"Nobody can get the reminder: {string.Join("; ", reasons)}.";
    }

    /// <summary>
    /// The students themselves (not parents) and their parents, with the bot subscribed and reminders on.
    /// </summary>
    private async Task<List<Recipient>> FindRecipientsAsync(List<Guid> studentIds, CancellationToken cancellationToken)
    {
        var students = await context.TelegramSubscribers
            .AsNoTracking()
            .Where(subscriber => studentIds.Contains(subscriber.Id) && !subscriber.IsParent && subscriber.IsActive && subscriber.RemindersEnabled)
            .Select(subscriber => new Recipient(subscriber.Id, subscriber.TelegramUserId, subscriber.ChatId, subscriber.FirstName, subscriber.Id, null))
            .ToListAsync(cancellationToken);

        var parents = await context.TelegramSubscribers
            .AsNoTracking()
            .Where(parent => parent.IsParent && parent.IsActive && parent.RemindersEnabled)
            .SelectMany(parent => parent.Children
                .Where(child => studentIds.Contains(child.Id))
                .Select(child => new Recipient(parent.Id, parent.TelegramUserId, parent.ChatId, parent.FirstName, child.Id, child.FirstName)))
            .ToListAsync(cancellationToken);

        return [.. students, .. parents];
    }

    private async Task<LessonReminderResult> SendAsync(
        List<Recipient> recipients,
        List<DueLesson> due,
        bool isManual,
        CancellationToken cancellationToken)
    {
        if (recipients.Count == 0)
            return new LessonReminderResult(0, 0, 0);

        var studentTemplate = (await templateService.GetAsync(MessageTemplateKind.PaymentReminder, cancellationToken)).Text;
        var parentTemplate = (await templateService.GetAsync(MessageTemplateKind.ParentPaymentReminder, cancellationToken)).Text;
        var lessonsByStudent = due.GroupBy(lesson => lesson.StudentId).ToDictionary(group => group.Key, group => group.ToList());
        var payment = PaymentInfo.From(await settingsService.GetAsync(cancellationToken));
        var payButton = PaymentTexts.PayButton(payment);
        var maxLines = paymentOptions.Value.MaxListedLessons;
        var delay = TimeSpan.FromSeconds(1.0 / reminderOptions.Value.MessagesPerSecond);

        var unreachable = new HashSet<long>();
        int sent = 0, failed = 0;

        foreach (var recipient in recipients)
        {
            if (unreachable.Contains(recipient.TelegramUserId) || !lessonsByStudent.TryGetValue(recipient.StudentId, out var lessons))
                continue;

            var text = PaymentTexts.Render(
                recipient.ChildName is null ? studentTemplate : parentTemplate,
                recipient.FirstName,
                recipient.ChildName,
                lessons,
                maxLines,
                payment);

            switch (await TrySendAsync(recipient, text, payButton, cancellationToken))
            {
                case true:
                    sent++;
                    context.PaymentReminderDeliveries.Add(new PaymentReminderDelivery
                    {
                        Id = Guid.CreateVersion7(),
                        SubscriberId = recipient.SubscriberId,
                        StudentId = recipient.StudentId,
                        SentAtUtc = clock.UtcNow,
                        IsManual = isManual
                    });
                    // not cancellable: the message is out, it must not be sent again
                    await context.SaveChangesAsync(CancellationToken.None);
                    break;
                case null:
                    unreachable.Add(recipient.TelegramUserId);
                    await context.TelegramSubscribers
                        .Where(subscriber => subscriber.TelegramUserId == recipient.TelegramUserId && subscriber.IsActive)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(subscriber => subscriber.IsActive, false)
                            .SetProperty(subscriber => subscriber.UnsubscribedAtUtc, clock.UtcNow), CancellationToken.None);
                    break;
                default:
                    failed++;
                    break;
            }

            await Task.Delay(delay, cancellationToken);
        }

        return new LessonReminderResult(sent, failed, unreachable.Count);
    }

    /// <returns>True when sent, false when it failed, null when the recipient blocked the bot or is gone.</returns>
    private async Task<bool?> TrySendAsync(Recipient recipient, string text, InlineKeyboardMarkup? payButton, CancellationToken cancellationToken)
    {
        // one retry is enough: Telegram says exactly how long to wait
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await botClient.SendMessage(recipient.ChatId, text, ParseMode.Html, replyMarkup: payButton, cancellationToken: cancellationToken);
                return true;
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == (int)HttpStatusCode.TooManyRequests && attempt == 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(ex.Parameters?.RetryAfter ?? 1), cancellationToken);
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == (int)HttpStatusCode.Forbidden
                                                 || (ex.ErrorCode == (int)HttpStatusCode.BadRequest
                                                     && ex.Message.Contains("chat not found", StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }
            catch (Exception ex) when (ex is RequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                LogSendFailed(logger, ex, recipient.TelegramUserId);
                return false;
            }
        }
    }

    /// <param name="ChildName">Set for a parent: the student the message is about.</param>
    private sealed record Recipient(Guid SubscriberId, long TelegramUserId, long ChatId, string FirstName, Guid StudentId, string? ChildName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Payment reminders: {Sent} sent, {Failed} failed, {Unreachable} unreachable")]
    private static partial void LogPaymentRemindersSent(ILogger logger, int sent, int failed, int unreachable);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not send a payment reminder to Telegram user {TelegramUserId}")]
    private static partial void LogSendFailed(ILogger logger, Exception exception, long telegramUserId);
}

/// <summary>
/// Used while the Telegram bot is turned off: nothing is sent, the "Remind" button explains why.
/// </summary>
public sealed class NullPaymentReminderService : IPaymentReminderService
{
    public Task<LessonReminderResult> SendDueAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new LessonReminderResult(0, 0, 0));

    public Task<LessonReminderResult> RemindStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        throw new ValidationFailedException("Telegram", "The Telegram bot is turned off (Telegram:Enabled), nothing can be sent.");

    public Task<PaymentRemindAllResult> RemindAllAsync(CancellationToken cancellationToken = default) =>
        throw new ValidationFailedException("Telegram", "The Telegram bot is turned off (Telegram:Enabled), nothing can be sent.");
}
