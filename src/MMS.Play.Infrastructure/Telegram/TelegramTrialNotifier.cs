using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MMS.Play.Infrastructure.Lessons;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;
using MMS.Play.Infrastructure.Trial;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// Trial lesson messages through the bot. A failed message is logged and skipped: the request is stored and
/// shown on the website anyway.
/// </summary>
public partial class TelegramTrialNotifier(
    ITelegramBotClient botClient,
    ApplicationDbContext context,
    SchoolClock clock,
    TrialEligibility eligibility,
    IOptions<PublicSiteSettings> publicSite,
    ILogger<TelegramTrialNotifier> logger) : ITrialNotifier
{
    // the admin page of the requests in the client
    private const string AdminPagePath = "/lessons/trial";

    public async Task NotifyAdminsAsync(TrialRequest request, CancellationToken cancellationToken)
    {
        var admins = await context.TelegramSubscribers
            .AsNoTracking()
            .Where(subscriber => subscriber.IsAdmin && subscriber.IsActive)
            .Select(subscriber => subscriber.ChatId)
            .ToListAsync(cancellationToken);

        if (admins.Count == 0)
            LogNoAdmins(logger, request.Id);

        var site = publicSite.Value;
        var keyboard = site.CanUseLinkButtons
            ? new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl("Відкрити заявки", site.Url(AdminPagePath)))
            : null;
        var text = TrialView.AdminText(request, clock.Today);

        foreach (var chatId in admins)
            await SendAsync(chatId, text, keyboard, cancellationToken);
    }

    public async Task NotifyBookedAsync(TrialRequest request, Lesson lesson, CancellationToken cancellationToken)
    {
        if (request.Subscriber is not { IsActive: true } subscriber)
            return;

        // they have a lesson now: the menu loses the trial button
        var keyboard = await eligibility.KeyboardAsync(subscriber.TelegramUserId, cancellationToken);
        await SendAsync(subscriber.ChatId, TrialView.BookedText(lesson, clock.Today), keyboard, cancellationToken);
    }

    public async Task NotifyDeclinedAsync(TrialRequest request, string? reason, CancellationToken cancellationToken)
    {
        if (request.Subscriber is not { IsActive: true } subscriber)
            return;

        // a declined request no longer blocks a new one, the menu may get the button back
        var keyboard = await eligibility.KeyboardAsync(subscriber.TelegramUserId, cancellationToken);
        await SendAsync(subscriber.ChatId, TrialView.DeclinedText(reason), keyboard, cancellationToken);
    }

    private async Task SendAsync(long chatId, string text, ReplyMarkup? keyboard, CancellationToken cancellationToken)
    {
        try
        {
            await botClient.SendMessage(chatId, text, ParseMode.Html, replyMarkup: keyboard, cancellationToken: cancellationToken);
        }
        catch (RequestException ex)
        {
            LogSendFailed(logger, ex, chatId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Trial lesson request {RequestId}: no bot admin to tell, it waits on the website")]
    private static partial void LogNoAdmins(ILogger logger, Guid requestId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not send a trial lesson message to chat {ChatId}")]
    private static partial void LogSendFailed(ILogger logger, Exception exception, long chatId);
}
