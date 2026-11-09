using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MMS.Play.Infrastructure.Lessons;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// Reschedule messages through the bot. A failed message is logged and skipped: the request itself
/// is stored and can always be answered on the website.
/// </summary>
public partial class TelegramRescheduleNotifier(
    ITelegramBotClient botClient,
    ApplicationDbContext context,
    SchoolClock clock,
    ILogger<TelegramRescheduleNotifier> logger) : IRescheduleNotifier
{
    public async Task<List<TelegramMessageRef>> NotifyAdminsAsync(RescheduleRequestInfo request, CancellationToken cancellationToken)
    {
        var admins = await context.TelegramSubscribers
            .AsNoTracking()
            .Where(subscriber => subscriber.IsAdmin && subscriber.IsActive)
            .Select(subscriber => subscriber.ChatId)
            .ToListAsync(cancellationToken);

        if (admins.Count == 0)
            LogNoAdmins(logger, request.RequestId);

        var view = RescheduleView.AdminRequestView(request);
        var messages = new List<TelegramMessageRef>();

        foreach (var chatId in admins)
        {
            try
            {
                var message = await botClient.SendMessage(chatId, view.Text, ParseMode.Html, replyMarkup: view.Keyboard, cancellationToken: cancellationToken);
                messages.Add(new TelegramMessageRef { ChatId = chatId, MessageId = message.Id });
            }
            catch (RequestException ex)
            {
                LogSendFailed(logger, ex, chatId);
            }
        }

        return messages;
    }

    public async Task UpdateAdminsAsync(
        IReadOnlyList<TelegramMessageRef> messages,
        RescheduleRequestInfo request,
        string outcome,
        CancellationToken cancellationToken)
    {
        var text = RescheduleView.AdminOutcomeText(request, outcome);

        foreach (var message in messages)
        {
            try
            {
                // without a keyboard: the buttons go away, nobody answers twice
                await botClient.EditMessageText(message.ChatId, message.MessageId, text, ParseMode.Html, cancellationToken: cancellationToken);
            }
            catch (RequestException ex)
            {
                // deleted by the admin, or older than Telegram lets edit: nothing to update
                LogSendFailed(logger, ex, message.ChatId);
            }
        }
    }

    public Task NotifyStudentApprovedAsync(RescheduleRequestInfo request, CancellationToken cancellationToken) =>
        SendAsync(request.StudentChatId, new RescheduleView.View(RescheduleView.ApprovedText(request, clock.Today), null), cancellationToken);

    public Task NotifyStudentDeclinedAsync(RescheduleRequestInfo request, bool slotTaken, RescheduleOptions? options, CancellationToken cancellationToken)
    {
        var intro = RescheduleView.DeclinedIntro(request, slotTaken);

        // a new message, not an edit: only new messages make the phone ring
        var view = options is null
            ? new RescheduleView.View(intro + "\n\nЦей урок уже не можна перенести, напишіть адміністратору.", null)
            : RescheduleView.DaysView(options, clock.Today, intro);

        return SendAsync(request.StudentChatId, view, cancellationToken);
    }

    private async Task SendAsync(long chatId, RescheduleView.View view, CancellationToken cancellationToken)
    {
        try
        {
            await botClient.SendMessage(chatId, view.Text, ParseMode.Html, replyMarkup: view.Keyboard, cancellationToken: cancellationToken);
        }
        catch (RequestException ex)
        {
            LogSendFailed(logger, ex, chatId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reschedule request {RequestId}: no bot admin to ask, it waits on the website")]
    private static partial void LogNoAdmins(ILogger logger, Guid requestId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not send or update a reschedule message in chat {ChatId}")]
    private static partial void LogSendFailed(ILogger logger, Exception exception, long chatId);
}
