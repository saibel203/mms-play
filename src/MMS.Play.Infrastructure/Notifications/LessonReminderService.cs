using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;
using MMS.Play.Contracts.Models.Lessons;
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
/// Reminds students and their parents of the lessons. Every reminder time of the day (e.g. 09:00 and 13:00) is a slot;
/// at each slot every student with a lesson from today up to "days before" ahead, still to come, gets one message per
/// lesson, and so does every parent of that student (with the parent text, about the child). Each recipient needs
/// their own "Reminders" checkbox on; a parent is never reminded as a student, and the child does not have to use the bot.
/// Called often (every few minutes): a slot is served once it has passed, at most "MissedSlotGraceMinutes" late,
/// and <see cref="LessonReminderDelivery"/> rows keep a recipient from being reminded twice of a lesson for the same slot.
/// </summary>
public partial class LessonReminderService(
    ApplicationDbContext context,
    ITelegramBotClient botClient,
    IMessageTemplateService templateService,
    INotificationSettingsService settingsService,
    SchoolClock clock,
    IOptions<LessonReminderSettings> options,
    ILogger<LessonReminderService> logger) : ILessonReminderService
{
    private readonly LessonReminderSettings _settings = options.Value;
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo(options.Value.Culture);

    public async Task<LessonReminderResult> SendRemindersAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        if (!settings.RemindersEnabled)
            return new LessonReminderResult(0, 0, 0);

        var now = clock.Now;
        var today = DateOnly.FromDateTime(now);
        var grace = TimeSpan.FromMinutes(_settings.MissedSlotGraceMinutes);

        var dueSlots = settings.ReminderIntervalMinutes is { } interval
            ? new List<TimeOnly> { CurrentIntervalSlot(now, interval) }
            : settings.ReminderTimes
                .Where(time => today.ToDateTime(time) <= now && now - today.ToDateTime(time) <= grace)
                .ToList();

        if (dueSlots.Count == 0)
            return new LessonReminderResult(0, 0, 0);

        // read once per run: an admin editing the template mid-run does not mix two texts in one run
        var templates = new Templates(
            (await templateService.GetAsync(MessageTemplateKind.LessonReminder, cancellationToken)).Text,
            (await templateService.GetAsync(MessageTemplateKind.ParentLessonReminder, cancellationToken)).Text);
        var lastDate = today.AddDays(settings.ReminderDaysBefore);
        var total = new LessonReminderResult(0, 0, 0);

        foreach (var slotTime in dueSlots)
        {
            if (clock.ToUtc(today, slotTime) is not { } slotUtc)
                continue; // the time does not exist today (daylight saving change)

            var result = await SendSlotAsync(today, slotTime, slotUtc, lastDate, templates, cancellationToken);
            total = new LessonReminderResult(total.Sent + result.Sent, total.Failed + result.Failed, total.Unsubscribed + result.Unsubscribed);
        }

        return total;
    }

    /// <summary>
    /// Interval mode: the slot is the start of the current window, 14:37 with 5 minutes is 14:35.
    /// Each window reminds every lesson once.
    /// </summary>
    private static TimeOnly CurrentIntervalSlot(DateTime now, int intervalMinutes)
    {
        var minutes = (int)now.TimeOfDay.TotalMinutes / intervalMinutes * intervalMinutes;
        return new TimeOnly(minutes / 60, minutes % 60);
    }

    private async Task<LessonReminderResult> SendSlotAsync(
        DateOnly today,
        TimeOnly slotTime,
        DateTime slotUtc,
        DateOnly lastDate,
        Templates templates,
        CancellationToken cancellationToken)
    {
        var lessons = context.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.Status == LessonStatus.Scheduled && lesson.Student != null)
            .Where(lesson => lesson.Date >= today
                             && lesson.Date <= lastDate
                             // on the lesson day only while the lesson is still ahead
                             && (lesson.Date > today || lesson.StartTime > slotTime));

        // a parent marked as the student of a lesson (e.g. a test lesson) is not reminded as a student
        var toStudents = await lessons
            .Where(lesson => !lesson.Student!.IsParent && lesson.Student.IsActive && lesson.Student.RemindersEnabled)
            .Where(lesson => !context.LessonReminderDeliveries.Any(delivery =>
                delivery.LessonId == lesson.Id && delivery.SubscriberId == lesson.StudentId && delivery.SlotUtc == slotUtc))
            .Select(lesson => new Reminder(
                lesson.Id,
                lesson.Student!.Id,
                lesson.Student.TelegramUserId,
                lesson.Student.ChatId,
                lesson.Student.FirstName,
                null,
                lesson.Date,
                lesson.StartTime,
                lesson.Room.Name,
                lesson.Teacher.Name))
            .ToListAsync(cancellationToken);

        var toParents = await lessons
            .SelectMany(lesson => lesson.Student!.Parents
                .Where(parent => parent.IsParent && parent.IsActive && parent.RemindersEnabled)
                .Where(parent => !context.LessonReminderDeliveries.Any(delivery =>
                    delivery.LessonId == lesson.Id && delivery.SubscriberId == parent.Id && delivery.SlotUtc == slotUtc))
                .Select(parent => new Reminder(
                    lesson.Id,
                    parent.Id,
                    parent.TelegramUserId,
                    parent.ChatId,
                    parent.FirstName,
                    lesson.Student!.FirstName,
                    lesson.Date,
                    lesson.StartTime,
                    lesson.Room.Name,
                    lesson.Teacher.Name)))
            .ToListAsync(cancellationToken);

        var reminders = toStudents.Concat(toParents)
            .OrderBy(reminder => reminder.Date)
            .ThenBy(reminder => reminder.StartTime)
            .ToList();

        var delayBetweenMessages = TimeSpan.FromSeconds(1.0 / _settings.MessagesPerSecond);
        // one lesson of a user who turned out unreachable is enough to skip the rest of their lessons
        var unreachableUsers = new HashSet<long>();
        int sent = 0, failed = 0;

        foreach (var reminder in reminders)
        {
            if (unreachableUsers.Contains(reminder.TelegramUserId))
                continue;

            var text = Render(reminder.ChildName is null ? templates.Student : templates.Parent, reminder, today);
            switch (await SendAsync(reminder, text, cancellationToken))
            {
                case DeliveryOutcome.Sent:
                    sent++;
                    await RecordDeliveryAsync(reminder, slotUtc);
                    break;
                case DeliveryOutcome.Unreachable:
                    unreachableUsers.Add(reminder.TelegramUserId);
                    await UnsubscribeAsync(reminder.TelegramUserId);
                    break;
                default:
                    // not recorded: the next run within the grace period tries again
                    failed++;
                    break;
            }

            await Task.Delay(delayBetweenMessages, cancellationToken);
        }

        if (reminders.Count > 0)
            LogSlotServed(logger, slotTime, sent, failed, unreachableUsers.Count);

        return new LessonReminderResult(sent, failed, unreachableUsers.Count);
    }

    private string Render(string template, Reminder reminder, DateOnly today) => template
        // the template is HTML, a name like "<Max>" would break the markup
        .Replace(MessagePlaceholders.FirstName, WebUtility.HtmlEncode(reminder.FirstName), StringComparison.Ordinal)
        .Replace(MessagePlaceholders.LessonDay, UkrainianDates.When(reminder.Date, today), StringComparison.Ordinal)
        .Replace(MessagePlaceholders.LessonDate, reminder.Date.ToString("dddd, dd.MM", _culture), StringComparison.Ordinal)
        .Replace(MessagePlaceholders.LessonTime, reminder.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace(MessagePlaceholders.Room, WebUtility.HtmlEncode(reminder.Room), StringComparison.Ordinal)
        .Replace(MessagePlaceholders.Teacher, WebUtility.HtmlEncode(reminder.Teacher), StringComparison.Ordinal)
        .Replace(ParentBroadcastLimits.ChildNamePlaceholder, WebUtility.HtmlEncode(reminder.ChildName ?? string.Empty), StringComparison.Ordinal);

    private async Task<DeliveryOutcome> SendAsync(Reminder reminder, string text, CancellationToken cancellationToken)
    {
        // one retry is enough: Telegram says exactly how long to wait
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await botClient.SendMessage(
                    reminder.ChatId,
                    text,
                    ParseMode.Html,
                    // only the student can move a lesson, the reschedule flow checks it is theirs; a parent can cancel it
                    replyMarkup: reminder.ChildName is null
                        ? RescheduleView.ReminderButtons(reminder.LessonId)
                        : new InlineKeyboardMarkup(CancellationView.AskButton(reminder.LessonId)),
                    cancellationToken: cancellationToken);
                return DeliveryOutcome.Sent;
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == (int)HttpStatusCode.TooManyRequests && attempt == 1)
            {
                var retryAfter = TimeSpan.FromSeconds(ex.Parameters?.RetryAfter ?? 1);
                LogRateLimited(logger, retryAfter);
                await Task.Delay(retryAfter, cancellationToken);
            }
            catch (ApiRequestException ex) when (IsUnreachable(ex))
            {
                LogRecipientUnreachable(logger, reminder.TelegramUserId, ex.Message);
                return DeliveryOutcome.Unreachable;
            }
            catch (Exception ex) when (ex is RequestException // ApiRequestException, and network errors wrapped by Telegram.Bot
                                       || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                LogSendFailed(logger, ex, reminder.LessonId, reminder.TelegramUserId);
                return DeliveryOutcome.Failed;
            }
        }
    }

    /// <summary>
    /// 403: the user blocked the bot or deleted the account. 400 "chat not found": the chat no longer exists.
    /// Sending again would fail the same way, so the subscriber is deactivated.
    /// </summary>
    private static bool IsUnreachable(ApiRequestException ex) =>
        ex.ErrorCode == (int)HttpStatusCode.Forbidden
        || (ex.ErrorCode == (int)HttpStatusCode.BadRequest && ex.Message.Contains("chat not found", StringComparison.OrdinalIgnoreCase));

    // Not cancellable: the message is already sent, a cancelled save would send it again on the next run.
    private async Task RecordDeliveryAsync(Reminder reminder, DateTime slotUtc)
    {
        var now = clock.UtcNow;

        context.LessonReminderDeliveries.Add(new LessonReminderDelivery
        {
            LessonId = reminder.LessonId,
            SubscriberId = reminder.SubscriberId,
            SlotUtc = slotUtc,
            SentAtUtc = now
        });
        await context.SaveChangesAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        await context.TelegramSubscribers
            .Where(subscriber => subscriber.TelegramUserId == reminder.TelegramUserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(subscriber => subscriber.LastNotifiedAtUtc, now), CancellationToken.None);
    }

    private async Task UnsubscribeAsync(long telegramUserId)
    {
        var now = clock.UtcNow;

        await context.TelegramSubscribers
            .Where(subscriber => subscriber.TelegramUserId == telegramUserId && subscriber.IsActive)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(subscriber => subscriber.IsActive, false)
                .SetProperty(subscriber => subscriber.UnsubscribedAtUtc, now), CancellationToken.None);
    }

    /// <param name="ChildName">Set for a parent: the student of the lesson. Null for the student.</param>
    private sealed record Reminder(
        Guid LessonId,
        Guid SubscriberId,
        long TelegramUserId,
        long ChatId,
        string FirstName,
        string? ChildName,
        DateOnly Date,
        TimeOnly StartTime,
        string Room,
        string Teacher);

    private sealed record Templates(string Student, string Parent);

    private enum DeliveryOutcome
    {
        Sent,
        Failed,
        Unreachable
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reminder slot {SlotTime}: {Sent} sent, {Failed} failed, {Unreachable} users unreachable")]
    private static partial void LogSlotServed(ILogger logger, TimeOnly slotTime, int sent, int failed, int unreachable);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Telegram rate limit hit, waiting {RetryAfter}")]
    private static partial void LogRateLimited(ILogger logger, TimeSpan retryAfter);

    [LoggerMessage(Level = LogLevel.Information, Message = "Telegram user {TelegramUserId} is unreachable ({Reason}), unsubscribed")]
    private static partial void LogRecipientUnreachable(ILogger logger, long telegramUserId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not send the reminder of lesson {LessonId} to Telegram user {TelegramUserId}")]
    private static partial void LogSendFailed(ILogger logger, Exception exception, Guid lessonId, long telegramUserId);
}
