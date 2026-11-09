using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Contracts.Models.Trial;
using MMS.Play.Infrastructure.Lessons;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;
using MMS.Play.Infrastructure.Telegram;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MMS.Play.Infrastructure.Notifications;

/// <summary>
/// The daily summary for the admins in the bot (subscribers with <see cref="TelegramSubscriber.IsAdmin"/>): today's
/// lessons, cancellations of the last day, open reschedule requests, debts, new trial requests and yesterday's practice.
/// Sent once a school day at the digest time, at most "MissedSlotGraceMinutes" late; an <see cref="AdminDigestDelivery"/>
/// row claims the day before anything is sent, so two runs never send it twice.
/// </summary>
public partial class AdminDigestService(
    ApplicationDbContext context,
    ITelegramBotClient botClient,
    INotificationSettingsService settingsService,
    SchoolClock clock,
    IOptions<LessonReminderSettings> reminderOptions,
    IOptions<PublicSiteSettings> siteOptions,
    ILogger<AdminDigestService> logger) : IAdminDigestService
{
    // a summary is a glance, not a report: a busy day is cut and counted
    private const int MaxListedLessons = 25;
    private const int MaxListedCancellations = 10;

    public async Task SendDueAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        if (!settings.AdminDigestEnabled)
            return;

        var now = clock.Now;
        var today = DateOnly.FromDateTime(now);
        var slot = today.ToDateTime(settings.AdminDigestTime);
        if (now < slot || now - slot > TimeSpan.FromMinutes(reminderOptions.Value.MissedSlotGraceMinutes))
            return;

        if (await context.AdminDigestDeliveries.AnyAsync(x => x.Date == today, cancellationToken))
            return;

        var admins = await FindAdminsAsync(cancellationToken);
        if (admins.Count == 0)
            return;

        // claimed before sending: a failure later means no digest today rather than several
        context.AdminDigestDeliveries.Add(new AdminDigestDelivery { Date = today, SentAtUtc = clock.UtcNow });
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // another run claimed the day a moment earlier
            return;
        }

        var sent = await SendAsync(admins, await BuildAsync(cancellationToken), cancellationToken);
        LogDigestSent(logger, sent, admins.Count);
    }

    public async Task<int> SendNowAsync(CancellationToken cancellationToken = default)
    {
        var admins = await FindAdminsAsync(cancellationToken);
        if (admins.Count == 0)
            throw new ValidationFailedException(
                "Admins", "Nobody gets the summary: mark a subscriber as admin on the Telegram page (they must be subscribed in the bot).");

        var sent = await SendAsync(admins, await BuildAsync(cancellationToken), cancellationToken);
        if (sent == 0)
            throw new ValidationFailedException("Admins", "Telegram did not take the messages, try again later.");

        LogDigestSent(logger, sent, admins.Count);
        return sent;
    }

    private Task<List<Admin>> FindAdminsAsync(CancellationToken cancellationToken) =>
        context.TelegramSubscribers
            .AsNoTracking()
            .Where(subscriber => subscriber.IsAdmin && subscriber.IsActive)
            .Select(subscriber => new Admin(subscriber.TelegramUserId, subscriber.ChatId))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// The summary as Telegram HTML, as of now.
    /// </summary>
    private async Task<Digest> BuildAsync(CancellationToken cancellationToken)
    {
        var now = clock.Now;
        var today = DateOnly.FromDateTime(now);
        var dayAgoUtc = clock.UtcNow.AddDays(-1);

        var lessons = await context.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.Date == today && lesson.Status != LessonStatus.Rescheduled)
            .OrderBy(lesson => lesson.StartTime)
            .ThenBy(lesson => lesson.Room.SortOrder)
            .Select(lesson => new
            {
                lesson.StartTime,
                lesson.Status,
                Student = lesson.Student == null ? null : lesson.Student.FirstName + " " + lesson.Student.LastName,
                lesson.Note,
                Teacher = lesson.Teacher.Name,
                Room = lesson.Room.Name
            })
            .ToListAsync(cancellationToken);

        var cancellations = await context.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.Status == LessonStatus.Cancelled && lesson.CancelledAtUtc > dayAgoUtc)
            .OrderBy(lesson => lesson.Date)
            .ThenBy(lesson => lesson.StartTime)
            .Select(lesson => new
            {
                lesson.Date,
                lesson.StartTime,
                Student = lesson.Student == null ? null : lesson.Student.FirstName + " " + lesson.Student.LastName,
                lesson.Note,
                lesson.CancelledByName,
                lesson.IsLateCancellation
            })
            .ToListAsync(cancellationToken);

        var pendingReschedules = await context.LessonRescheduleRequests
            .CountAsync(request => request.Status == RescheduleRequestStatus.Pending, cancellationToken);

        var due = await DueLessons.FindAsync(context, null, now, cancellationToken);
        var debtors = due.Select(lesson => lesson.StudentId).Distinct().Count();

        var newTrials = await context.TrialRequests
            .CountAsync(request => request.Status == TrialRequestStatus.New, cancellationToken);

        var yesterday = today.AddDays(-1);
        var practisedYesterday = await context.HomeworkPractices
            .Where(practice => practice.Date == yesterday)
            .Select(practice => practice.Homework.StudentId)
            .Distinct()
            .CountAsync(cancellationToken);

        var text = new StringBuilder();
        text.Append($"☀️ <b>Зведення на {UkrainianDates.ShortDay(today)}</b>\n\n");

        var held = lessons.Count(lesson => lesson.Status == LessonStatus.Scheduled);
        if (lessons.Count == 0)
        {
            text.Append("📅 Сьогодні уроків немає.\n");
        }
        else
        {
            text.Append($"📅 <b>Уроки сьогодні: {held}</b>\n");
            foreach (var lesson in lessons.Take(MaxListedLessons))
            {
                var line = $"{UkrainianDates.Time(lesson.StartTime)} {Encode(Who(lesson.Student, lesson.Note))} — "
                           + $"{Encode(lesson.Teacher)}, {Encode(lesson.Room)}";
                text.Append(lesson.Status == LessonStatus.Cancelled ? $"<s>{line}</s> (скасовано)\n" : $"{line}\n");
            }

            if (lessons.Count > MaxListedLessons)
                text.Append($"…і ще {lessons.Count - MaxListedLessons}\n");
        }

        if (cancellations.Count > 0)
        {
            text.Append($"\n❌ <b>Скасування за добу: {cancellations.Count}</b>\n");
            foreach (var lesson in cancellations.Take(MaxListedCancellations))
            {
                text.Append($"{UkrainianDates.ShortDayTime(lesson.Date, lesson.StartTime)} {Encode(Who(lesson.Student, lesson.Note))}");
                if (lesson.CancelledByName is { } by)
                    text.Append($" ({Encode(by)})");
                if (lesson.IsLateCancellation)
                    text.Append(" ⚠️ пізно");
                text.Append('\n');
            }

            if (cancellations.Count > MaxListedCancellations)
                text.Append($"…і ще {cancellations.Count - MaxListedCancellations}\n");
        }

        text.Append('\n');
        if (pendingReschedules > 0)
            text.Append($"🔁 Запити на перенесення без відповіді: <b>{pendingReschedules}</b>\n");
        if (newTrials > 0)
            text.Append($"🎸 Нові заявки на пробний урок: <b>{newTrials}</b>\n");

        text.Append(due.Count > 0
            ? $"💳 Борг: <b>{PaymentTexts.Money(due.Sum(lesson => lesson.Price))}</b> ({debtors} {Students(debtors)}, {due.Count} {Lessons(due.Count)})\n"
            : "💳 Боргів немає.\n");
        text.Append($"📚 Учора займались: <b>{practisedYesterday}</b> {Students(practisedYesterday)}\n");

        var site = siteOptions.Value;
        InlineKeyboardMarkup? button = null;
        if (site.CanUseLinkButtons)
            button = new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl("🗓 Відкрити розклад", site.Url("/lessons")));
        else if (site.IsConfigured)
            text.Append($"\n🗓 Розклад: {Encode(site.Url("/lessons"))}");

        return new Digest(text.ToString().TrimEnd(), button);
    }

    /// <returns>How many admins got it.</returns>
    private async Task<int> SendAsync(List<Admin> admins, Digest digest, CancellationToken cancellationToken)
    {
        var sent = 0;
        foreach (var admin in admins)
        {
            try
            {
                await botClient.SendMessage(
                    admin.ChatId,
                    digest.Text,
                    ParseMode.Html,
                    replyMarkup: digest.Button,
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                    cancellationToken: cancellationToken);
                sent++;
            }
            catch (Exception ex) when (ex is RequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                // a blocked bot or a network error: the other admins still get it, the lesson reminders unsubscribe the blocked
                LogSendFailed(logger, ex, admin.TelegramUserId);
            }
        }

        return sent;
    }

    private static string Who(string? student, string? note) =>
        !string.IsNullOrWhiteSpace(student) ? student.Trim()
        : !string.IsNullOrWhiteSpace(note) ? note.Trim()
        : "без учня";

    private static string Encode(string text) => WebUtility.HtmlEncode(text);

    // "1 учень", "3 учні", "5 учнів"
    private static string Students(int count) => Plural(count, "учень", "учні", "учнів");

    // "1 урок", "3 уроки", "5 уроків"
    private static string Lessons(int count) => Plural(count, "урок", "уроки", "уроків");

    private static string Plural(int count, string one, string few, string many)
    {
        var lastTwo = count % 100;
        var last = count % 10;
        return lastTwo is >= 11 and <= 14 ? many
            : last == 1 ? one
            : last is >= 2 and <= 4 ? few
            : many;
    }

    private sealed record Admin(long TelegramUserId, long ChatId);

    private sealed record Digest(string Text, InlineKeyboardMarkup? Button);

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin digest sent to {Sent} of {Admins} admins")]
    private static partial void LogDigestSent(ILogger logger, int sent, int admins);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not send the admin digest to Telegram user {TelegramUserId}")]
    private static partial void LogSendFailed(ILogger logger, Exception exception, long telegramUserId);
}

/// <summary>
/// Used while the Telegram bot is turned off: nothing is sent, "Send now" explains why.
/// </summary>
public sealed class NullAdminDigestService : IAdminDigestService
{
    public Task SendDueAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<int> SendNowAsync(CancellationToken cancellationToken = default) =>
        throw new ValidationFailedException("Telegram", "The Telegram bot is turned off (Telegram:Enabled), nothing can be sent.");
}
