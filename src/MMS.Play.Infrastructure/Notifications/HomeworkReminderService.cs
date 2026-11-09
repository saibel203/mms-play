using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;
using MMS.Play.Contracts.Models.Homework;
using MMS.Play.Contracts.Models.Notifications;
using MMS.Play.Infrastructure.Homeworks;
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
/// The daily «time to practise» message: at the homework reminder time, one message to every student (subscribed,
/// "Reminders" on, not a parent) with active homework not marked practised today, listing that homework with a
/// «✅ Позаймався» button each. <see cref="HomeworkReminderDelivery"/> rows make it once a day per student.
/// </summary>
public partial class HomeworkReminderService(
    ApplicationDbContext context,
    ITelegramBotClient botClient,
    IMessageTemplateService templateService,
    INotificationSettingsService settingsService,
    PracticeLinks practiceLinks,
    SchoolClock clock,
    IOptions<LessonReminderSettings> reminderOptions,
    IOptions<HomeworkReminderSettings> homeworkOptions,
    ILogger<HomeworkReminderService> logger) : IHomeworkReminderService
{
    private static readonly LessonReminderResult Nothing = new(0, 0, 0);

    public async Task<LessonReminderResult> SendDueAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        if (!settings.HomeworkRemindersEnabled)
            return Nothing;

        var now = clock.Now;
        var today = DateOnly.FromDateTime(now);
        var slot = today.ToDateTime(settings.HomeworkReminderTime);
        if (now < slot || now - slot > TimeSpan.FromMinutes(reminderOptions.Value.MissedSlotGraceMinutes))
            return Nothing;

        var remindedToday = context.HomeworkReminderDeliveries.Where(x => x.Date == today).Select(x => x.SubscriberId);
        var rows = await HomeworkRow.ListAsync(
            context.Homeworks
                .Where(x => x.Status == HomeworkStatus.Active
                            && x.Student.IsActive && x.Student.RemindersEnabled && !x.Student.IsParent
                            && !x.Practices.Any(practice => practice.Date == today)
                            && !remindedToday.Contains(x.StudentId))
                .OrderBy(x => x.CreatedAtUtc),
            cancellationToken);

        if (rows.Count == 0)
            return Nothing;

        var result = await SendAsync(rows, today, cancellationToken);
        LogHomeworkRemindersSent(logger, result.Sent, result.Failed, result.Unsubscribed);
        return result;
    }

    private async Task<LessonReminderResult> SendAsync(List<HomeworkRow> rows, DateOnly today, CancellationToken cancellationToken)
    {
        var template = (await templateService.GetAsync(MessageTemplateKind.HomeworkReminder, cancellationToken)).Text;
        var maxListed = homeworkOptions.Value.MaxListed;
        var delay = TimeSpan.FromSeconds(1.0 / reminderOptions.Value.MessagesPerSecond);
        int sent = 0, failed = 0, unreachable = 0;

        foreach (var group in rows.GroupBy(row => row.StudentId))
        {
            var student = await context.TelegramSubscribers.FirstAsync(x => x.Id == group.Key, cancellationToken);
            var homework = group.ToList();
            var listed = homework.Take(maxListed).ToList();

            // the streak of the student, whichever homework they practised
            var practiceDays = await context.HomeworkPractices
                .Where(x => x.Homework.StudentId == student.Id && x.Date >= today.AddDays(-366))
                .Select(x => x.Date)
                .Distinct()
                .ToListAsync(cancellationToken);

            var link = await practiceLinks.ForAsync(student, cancellationToken);
            var text = HomeworkTexts.Render(
                template,
                student.FirstName,
                ListHtml(listed, homework.Count),
                string.Empty,
                HomeworkStats.Streak(practiceDays, today),
                HomeworkTexts.PracticeLinkHtml(link, practiceLinks.CanUseLinkButtons));
            var keyboard = HomeworkTexts.Keyboard(listed, link, practiceLinks.CanUseLinkButtons);

            switch (await TrySendAsync(student.ChatId, student.TelegramUserId, text, keyboard, cancellationToken))
            {
                case true:
                    sent++;
                    context.HomeworkReminderDeliveries.Add(new HomeworkReminderDelivery
                    {
                        SubscriberId = student.Id,
                        Date = today,
                        SentAtUtc = clock.UtcNow
                    });
                    // not cancellable: the message is out, it must not be sent again
                    await context.SaveChangesAsync(CancellationToken.None);
                    break;
                case null:
                    unreachable++;
                    student.IsActive = false;
                    student.UnsubscribedAtUtc = clock.UtcNow;
                    await context.SaveChangesAsync(CancellationToken.None);
                    break;
                default:
                    failed++;
                    break;
            }

            await Task.Delay(delay, cancellationToken);
        }

        return new LessonReminderResult(sent, failed, unreachable);
    }

    /// <summary>
    /// Each homework described, blank lines between them; past the listed ones "…і ще 2 на сторінці занять".
    /// </summary>
    private static string ListHtml(List<HomeworkRow> listed, int total)
    {
        var text = new StringBuilder(string.Join("\n\n", listed.Select(row => HomeworkTexts.Describe(row))));
        if (total > listed.Count)
            text.Append($"\n\n…і ще {total - listed.Count} — усе є на сторінці занять і в /homework");
        return text.ToString();
    }

    /// <returns>True when sent, false when it failed, null when the student blocked the bot or is gone.</returns>
    private async Task<bool?> TrySendAsync(long chatId, long telegramUserId, string text, InlineKeyboardMarkup? keyboard, CancellationToken cancellationToken)
    {
        // one retry is enough: Telegram says exactly how long to wait
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await botClient.SendMessage(chatId, text, ParseMode.Html, replyMarkup: keyboard, cancellationToken: cancellationToken);
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
                LogSendFailed(logger, ex, telegramUserId);
                return false;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Homework reminders: {Sent} sent, {Failed} failed, {Unreachable} unreachable")]
    private static partial void LogHomeworkRemindersSent(ILogger logger, int sent, int failed, int unreachable);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not send a homework reminder to Telegram user {TelegramUserId}")]
    private static partial void LogSendFailed(ILogger logger, Exception exception, long telegramUserId);
}

/// <summary>
/// Used while the Telegram bot is turned off: nothing is sent.
/// </summary>
public sealed class NullHomeworkReminderService : IHomeworkReminderService
{
    public Task<LessonReminderResult> SendDueAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new LessonReminderResult(0, 0, 0));
}
