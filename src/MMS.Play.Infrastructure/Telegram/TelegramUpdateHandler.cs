using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MMS.Play.Infrastructure.Trial;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// Handles updates received from Telegram: /start subscribes, /stop unsubscribes, blocking the bot unsubscribes too;
/// /payments shows the unpaid lessons and /pay what to pay and where (<see cref="PaymentBotHandler"/>);
/// the buttons of <see cref="BotMenu"/> under the message field stand for the commands; /lessons and the buttons of the reschedule flow go to <see cref="RescheduleBotHandler"/>; /clear deletes the recent messages;
/// answers to the questions sent to parents (buttons and typed text) go to <see cref="ParentBroadcastBotHandler"/>;
/// /trial and its buttons go to <see cref="TrialBotHandler"/>, /calendar to <see cref="CalendarBotHandler"/>.
/// The menu keyboard is built per user: newcomers also get «🎸 Пробний урок» (<see cref="TrialEligibility"/>).
/// Only private chats are handled, the bot is not meant for groups.
/// </summary>
public partial class TelegramUpdateHandler(IServiceScopeFactory scopeFactory, ILogger<TelegramUpdateHandler> logger) : IUpdateHandler
{
    private const string StartCommand = BotMenu.StartCommand;
    private const string StopCommand = BotMenu.StopCommand;
    private const string LessonsCommand = BotMenu.LessonsCommand;
    private const string ClearCommand = BotMenu.ClearCommand;
    private const string PaymentsCommand = BotMenu.PaymentsCommand;
    private const string PayCommand = BotMenu.PayCommand;

    // how far back /clear reaches; Telegram lets a bot delete only messages younger than 48 hours anyway
    private const int ClearMessageCount = 1000;
    // DeleteMessages takes at most 100 ids per call
    private const int DeleteBatchSize = 100;

    // a wrong token or no network makes getUpdates fail instantly, without a pause the receiver would spin
    private static readonly TimeSpan PollingErrorDelay = TimeSpan.FromSeconds(5);

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        switch (update)
        {
            case { Message: { Text: { } text, From: { } from, Chat.Type: ChatType.Private } message }:
                await HandleCommandAsync(botClient, message, from, text, cancellationToken);
                break;

            // answer buttons of a broadcast for parents
            case { CallbackQuery: { Data: { } data } query } when ParentBroadcastView.IsAnswerCallback(data):
                using (var scope = scopeFactory.CreateScope())
                {
                    var broadcasts = scope.ServiceProvider.GetRequiredService<ParentBroadcastBotHandler>();
                    await broadcasts.HandleCallbackAsync(botClient, query, cancellationToken);
                }
                break;

            // cancelling a lesson
            case { CallbackQuery: { Data: { } data } query } when CancellationView.IsCancellationCallback(data):
                using (var scope = scopeFactory.CreateScope())
                {
                    var cancellation = scope.ServiceProvider.GetRequiredService<CancellationBotHandler>();
                    await cancellation.HandleCallbackAsync(botClient, query, cancellationToken);
                }
                break;

            // booking a trial lesson
            case { CallbackQuery: { Data: { } data } query } when TrialView.IsTrialCallback(data):
                using (var scope = scopeFactory.CreateScope())
                {
                    var trial = scope.ServiceProvider.GetRequiredService<TrialBotHandler>();
                    await trial.HandleCallbackAsync(botClient, query, cancellationToken);
                }
                break;

            // replacing the calendar link
            case { CallbackQuery: { Data: { } data } query } when CalendarBotHandler.IsCalendarCallback(data):
                using (var scope = scopeFactory.CreateScope())
                {
                    var calendar = scope.ServiceProvider.GetRequiredService<CalendarBotHandler>();
                    await calendar.HandleCallbackAsync(botClient, query, cancellationToken);
                }
                break;

            // «✅ Позаймався» under homework
            case { CallbackQuery: { Data: { } data } query } when HomeworkTexts.IsHomeworkCallback(data):
                using (var scope = scopeFactory.CreateScope())
                {
                    var homework = scope.ServiceProvider.GetRequiredService<HomeworkBotHandler>();
                    await homework.HandleCallbackAsync(botClient, query, cancellationToken);
                }
                break;

            // the other buttons under the bot's messages: the reschedule flow
            case { CallbackQuery: { } query }:
                using (var scope = scopeFactory.CreateScope())
                {
                    var reschedule = scope.ServiceProvider.GetRequiredService<RescheduleBotHandler>();
                    await reschedule.HandleCallbackAsync(botClient, query, cancellationToken);
                }
                break;

            // sent when the user blocks (Kicked) or unblocks (Member) the bot, unblocking is followed by /start anyway
            case { MyChatMember: { Chat.Type: ChatType.Private, NewChatMember.Status: ChatMemberStatus.Kicked } member }:
                await UnsubscribeAsync(member.From.Id, cancellationToken);
                LogBotBlocked(logger, member.From.Id);
                break;
        }
    }

    public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        LogUpdateError(logger, exception, source);

        if (source == HandleErrorSource.PollingError)
            await Task.Delay(PollingErrorDelay, cancellationToken);
    }

    private async Task HandleCommandAsync(ITelegramBotClient botClient, Message message, User from, string text, CancellationToken cancellationToken)
    {
        string reply;
        ReplyMarkup replyMarkup = BotMenu.Keyboard;
        var chatId = message.Chat.Id;
        // a menu button sends its label, e.g. «💳 Мій борг» stands for /payments
        var menuCommand = BotMenu.ToCommand(text);
        var command = menuCommand ?? ParseCommand(text);

        if (command == ClearCommand)
        {
            await ClearAsync(botClient, chatId, message.Id, cancellationToken);
            return;
        }

        // not a command: maybe a parent typing an answer to a question
        if (menuCommand is null && !text.StartsWith('/'))
        {
            using var scope = scopeFactory.CreateScope();
            var broadcasts = scope.ServiceProvider.GetRequiredService<ParentBroadcastBotHandler>();
            if (await broadcasts.TryAnswerWithTextAsync(botClient, message, text, cancellationToken))
                return;
        }

        if (command == PaymentsCommand)
        {
            using var scope = scopeFactory.CreateScope();
            var payments = scope.ServiceProvider.GetRequiredService<PaymentBotHandler>();
            await payments.HandleAsync(botClient, chatId, from.Id, cancellationToken);
            return;
        }

        if (command == PayCommand)
        {
            using var scope = scopeFactory.CreateScope();
            var payments = scope.ServiceProvider.GetRequiredService<PaymentBotHandler>();
            await payments.HandlePayAsync(botClient, chatId, from.Id, cancellationToken);
            return;
        }

        if (command == LessonsCommand)
        {
            using var scope = scopeFactory.CreateScope();
            var reschedule = scope.ServiceProvider.GetRequiredService<RescheduleBotHandler>();
            await reschedule.HandleLessonsCommandAsync(botClient, chatId, from, cancellationToken);
            return;
        }

        if (command == BotMenu.TrialCommand)
        {
            using var scope = scopeFactory.CreateScope();
            var trial = scope.ServiceProvider.GetRequiredService<TrialBotHandler>();
            await trial.HandleCommandAsync(botClient, chatId, from.Id, cancellationToken);
            return;
        }

        if (command == BotMenu.HomeworkCommand)
        {
            using var scope = scopeFactory.CreateScope();
            var homework = scope.ServiceProvider.GetRequiredService<HomeworkBotHandler>();
            await homework.HandleCommandAsync(botClient, chatId, from.Id, cancellationToken);
            return;
        }

        if (command == BotMenu.CalendarCommand)
        {
            using var scope = scopeFactory.CreateScope();
            var calendar = scope.ServiceProvider.GetRequiredService<CalendarBotHandler>();
            await calendar.HandleCommandAsync(botClient, chatId, from.Id, cancellationToken);
            return;
        }

        switch (command)
        {
            case StartCommand:
                bool isNew;
                bool canBookTrial;
                string? inviteResult = null;
                using (var scope = scopeFactory.CreateScope())
                {
                    var subscriptions = scope.ServiceProvider.GetRequiredService<TelegramSubscriberService>();
                    isNew = await subscriptions.SubscribeAsync(from, chatId, cancellationToken);

                    // "t.me/bot?start=inv_…" arrives as "/start inv_…": an invite link
                    if (ParsePayload(text) is { } payload && payload.StartsWith(InviteService.PayloadPrefix, StringComparison.Ordinal))
                    {
                        var invites = scope.ServiceProvider.GetRequiredService<InviteService>();
                        inviteResult = await invites.RedeemAsync(payload[InviteService.PayloadPrefix.Length..], from.Id, cancellationToken);
                    }

                    // after the invite: becoming a parent makes the trial button go away
                    canBookTrial = await IsNewcomerAsync(scope, from.Id, cancellationToken);
                }

                LogSubscribed(logger, from.Id, isNew);
                replyMarkup = BotMenu.KeyboardFor(canBookTrial);
                reply = $"Вітаю, {WebUtility.HtmlEncode(from.FirstName)}! 👋\n\n"
                        + (inviteResult is null ? "" : inviteResult + "\n\n")
                        + (canBookTrial
                            ? $"Ви ще не займалися у нас? Запишіться на пробний урок — кнопка «{BotMenu.TrialButton}» внизу.\n\n"
                            : "")
                        + "Ви підписані на нагадування про уроки. Кнопки внизу — швидкий доступ до розкладу й оплати.\n\n"
                        + BotMenu.HelpText(withStart: false, canBookTrial);
                break;

            case StopCommand:
                await UnsubscribeAsync(from.Id, cancellationToken);
                LogUnsubscribed(logger, from.Id);
                reply = $"Ви відписалися від нагадувань. Щоб підписатися знову, надішліть {StartCommand}.";
                replyMarkup = new ReplyKeyboardRemove();
                break;

            // /help, «❓ Допомога» and anything the bot does not know
            default:
                using (var scope = scopeFactory.CreateScope())
                {
                    var newcomer = await IsNewcomerAsync(scope, from.Id, cancellationToken);
                    replyMarkup = BotMenu.KeyboardFor(newcomer);
                    reply = BotMenu.HelpText(withStart: true, newcomer);
                }
                break;
        }

        await botClient.SendMessage(chatId, reply, ParseMode.Html, replyMarkup: replyMarkup, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Deletes the last messages of the chat, the bot's and the user's, /clear included. In a private chat message ids
    /// count up one by one, so the ids just below the command are the recent messages; ids that are gone already are
    /// skipped by Telegram. Messages older than 48 hours cannot be deleted by a bot, the user clears those in the app.
    /// </summary>
    private async Task ClearAsync(ITelegramBotClient botClient, long chatId, int lastMessageId, CancellationToken cancellationToken)
    {
        var first = Math.Max(1, lastMessageId - ClearMessageCount + 1);
        var ids = Enumerable.Range(first, lastMessageId - first + 1).Reverse();
        var failed = false;

        foreach (var batch in ids.Chunk(DeleteBatchSize))
        {
            try
            {
                await botClient.DeleteMessages(chatId, batch, cancellationToken);
            }
            catch (ApiRequestException ex)
            {
                // a batch with messages too old to delete; the newer batches went through already
                failed = true;
                LogClearIncomplete(logger, chatId, ex.Message);
            }
        }

        if (failed)
        {
            await botClient.SendMessage(
                chatId,
                "🧹 Нові повідомлення прибрано. Старші за 48 годин бот видалити не може: "
                + "їх можна прибрати через меню чату → «Очистити історію».",
                cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Whether the menu gets «🎸 Пробний урок», see <see cref="TrialEligibility"/>.
    /// </summary>
    private static async Task<bool> IsNewcomerAsync(IServiceScope scope, long telegramUserId, CancellationToken cancellationToken) =>
        await scope.ServiceProvider.GetRequiredService<TrialEligibility>().CheckAsync(telegramUserId, cancellationToken)
        == TrialEligibility.Result.Eligible;

    private async Task UnsubscribeAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var subscriptions = scope.ServiceProvider.GetRequiredService<TelegramSubscriberService>();
        await subscriptions.UnsubscribeAsync(telegramUserId, cancellationToken);
    }

    /// <summary>
    /// "/start", "/start payload" and "/start@my_bot" all give "/start". Deep link payloads will carry account link tokens later.
    /// </summary>
    /// <summary>
    /// "/start inv_abc" gives "inv_abc", null without a payload.
    /// </summary>
    private static string? ParsePayload(string text)
    {
        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? parts[1].Trim() : null;
    }

    private static string ParseCommand(string text)
    {
        var command = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var botSuffix = command.IndexOf('@');

        return (botSuffix >= 0 ? command[..botSuffix] : command).ToLowerInvariant();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Telegram user {TelegramUserId} subscribed (new: {IsNew})")]
    private static partial void LogSubscribed(ILogger logger, long telegramUserId, bool isNew);

    [LoggerMessage(Level = LogLevel.Information, Message = "Telegram user {TelegramUserId} unsubscribed")]
    private static partial void LogUnsubscribed(ILogger logger, long telegramUserId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Telegram user {TelegramUserId} blocked the bot, unsubscribed")]
    private static partial void LogBotBlocked(ILogger logger, long telegramUserId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Chat {ChatId}: /clear could not delete some messages ({Reason})")]
    private static partial void LogClearIncomplete(ILogger logger, long chatId, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Telegram update handling failed ({Source})")]
    private static partial void LogUpdateError(ILogger logger, Exception exception, HandleErrorSource source);
}
