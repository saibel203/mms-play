using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MMS.Play.Infrastructure.Lessons;
using MMS.Play.Infrastructure.Persistence;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// Cancellation messages through the bot. A message that does not go out is logged and skipped:
/// the cancellation is stored and shows on the website anyway.
/// </summary>
public partial class TelegramCancellationNotifier(
    ITelegramBotClient botClient,
    ApplicationDbContext context,
    SchoolClock clock,
    ILogger<TelegramCancellationNotifier> logger) : ICancellationNotifier
{
    public async Task NotifyFamilyAsync(CancelledLessonInfo lesson, CancellationToken cancellationToken)
    {
        if (lesson.StudentId is not { } studentId)
            return;

        var family = await context.TelegramSubscribers
            .AsNoTracking()
            .Where(subscriber => subscriber.Id == studentId)
            .Select(student => new
            {
                Student = student.IsActive && !student.IsParent ? (long?)student.ChatId : null,
                student.FirstName,
                Parents = student.Parents.Where(parent => parent.IsParent && parent.IsActive).Select(parent => parent.ChatId).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (family is null)
            return;

        var today = clock.Today;
        if (family.Student is { } studentChat)
            await SendAsync(studentChat, CancellationView.FamilyText(lesson, null, today), cancellationToken);

        foreach (var parentChat in family.Parents)
            await SendAsync(parentChat, CancellationView.FamilyText(lesson, family.FirstName, today), cancellationToken);
    }

    public async Task NotifyAdminsAsync(CancelledLessonInfo lesson, CancellationToken cancellationToken)
    {
        var admins = await context.TelegramSubscribers
            .AsNoTracking()
            .Where(subscriber => subscriber.IsAdmin && subscriber.IsActive)
            .Select(subscriber => subscriber.ChatId)
            .ToListAsync(cancellationToken);

        var text = CancellationView.AdminText(lesson, clock.Today);
        foreach (var chatId in admins)
            await SendAsync(chatId, text, cancellationToken);
    }

    private async Task SendAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        try
        {
            await botClient.SendMessage(chatId, text, ParseMode.Html, cancellationToken: cancellationToken);
        }
        catch (RequestException ex)
        {
            LogSendFailed(logger, ex, chatId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not send a cancellation message to chat {ChatId}")]
    private static partial void LogSendFailed(ILogger logger, Exception exception, long chatId);
}
