using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MMS.Play.Contracts.Models.Notifications;
using MMS.Play.Infrastructure.Lessons;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// Parents answering a broadcast question in the bot: a button (<see cref="ParentBroadcastView.Answer"/>) or a typed message.
/// A typed message answers the question it replies to, otherwise the latest unanswered question of the chat
/// sent within <see cref="ParentBroadcastLimits.TextAnswerWindowDays"/> days. The last answer wins.
/// </summary>
public partial class ParentBroadcastBotHandler(ApplicationDbContext context, SchoolClock clock, ILogger<ParentBroadcastBotHandler> logger)
{
    public async Task HandleCallbackAsync(ITelegramBotClient botClient, CallbackQuery query, CancellationToken cancellationToken)
    {
        string? alert = null;
        string? toast = null;

        try
        {
            if (query.Message is { } message
                && query.Data is { } data
                && ParentBroadcastView.TryParseAnswer(data, out var deliveryId, out var optionIndex))
            {
                (toast, alert) = await AnswerWithButtonAsync(botClient, message, deliveryId, optionIndex, cancellationToken);
            }
        }
        finally
        {
            // always answered, otherwise the button keeps spinning on the user's phone
            await botClient.AnswerCallbackQuery(query.Id, alert ?? toast, showAlert: alert is not null, cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Records a typed message as an answer when there is a question waiting for one.
    /// </summary>
    /// <returns>False when no question takes a typed answer: the message is something else.</returns>
    public async Task<bool> TryAnswerWithTextAsync(ITelegramBotClient botClient, Message message, string text, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var deliveries = context.ParentBroadcastDeliveries
            .Include(delivery => delivery.Broadcast)
            .Where(delivery => delivery.ChatId == chatId
                               && delivery.Status == BroadcastDeliveryStatus.Sent
                               && delivery.Broadcast.AllowTextAnswer);

        ParentBroadcastDelivery? delivery;
        if (message.ReplyToMessage is { } repliedTo)
        {
            delivery = await deliveries.FirstOrDefaultAsync(x => x.MessageId == repliedTo.Id, cancellationToken);
        }
        else
        {
            var since = clock.UtcNow.AddDays(-ParentBroadcastLimits.TextAnswerWindowDays);
            delivery = await deliveries
                .Where(x => x.AnsweredAtUtc == null && x.SentAtUtc >= since)
                .OrderByDescending(x => x.SentAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (delivery is null)
            return false;

        var answer = text.Trim();
        if (answer.Length > ParentBroadcastLimits.AnswerMaxLength)
            answer = answer[..ParentBroadcastLimits.AnswerMaxLength];

        delivery.Answer = answer;
        delivery.AnswerOption = null;
        delivery.AnsweredAtUtc = clock.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        LogAnswered(logger, delivery.BroadcastId, delivery.ParentId);

        await ShowAnswerAsync(botClient, delivery, chosen: null, cancellationToken);
        await botClient.SendMessage(
            chatId,
            "✅ Дякуємо! Вашу відповідь збережено.",
            replyParameters: new ReplyParameters { MessageId = message.Id },
            cancellationToken: cancellationToken);
        return true;
    }

    private async Task<(string? Toast, string? Alert)> AnswerWithButtonAsync(
        ITelegramBotClient botClient,
        Message message,
        Guid deliveryId,
        int optionIndex,
        CancellationToken cancellationToken)
    {
        // the chat check keeps a forwarded message from answering for someone else
        var delivery = await context.ParentBroadcastDeliveries
            .Include(x => x.Broadcast)
            .FirstOrDefaultAsync(x => x.Id == deliveryId && x.ChatId == message.Chat.Id, cancellationToken);

        if (delivery is null || optionIndex < 0 || optionIndex >= delivery.Broadcast.Options.Count)
            return (null, "Це питання вже неактуальне.");

        if (delivery.AnswerOption == optionIndex)
            return ("Цю відповідь уже збережено ✅", null);

        delivery.Answer = delivery.Broadcast.Options[optionIndex];
        delivery.AnswerOption = optionIndex;
        delivery.AnsweredAtUtc = clock.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        LogAnswered(logger, delivery.BroadcastId, delivery.ParentId);

        await ShowAnswerAsync(botClient, delivery, optionIndex, cancellationToken);
        return ("Дякуємо! Відповідь збережено ✅", null);
    }

    /// <summary>
    /// Writes the answer under the question and ticks the pressed button.
    /// </summary>
    private async Task ShowAnswerAsync(ITelegramBotClient botClient, ParentBroadcastDelivery delivery, int? chosen, CancellationToken cancellationToken)
    {
        if (delivery.MessageId is not { } messageId)
            return;

        var broadcast = delivery.Broadcast;
        try
        {
            await botClient.EditMessageText(
                delivery.ChatId,
                messageId,
                ParentBroadcastView.Text(delivery.Text, broadcast.Options, broadcast.AllowTextAnswer, delivery.Answer),
                ParseMode.Html,
                replyMarkup: ParentBroadcastView.Keyboard(delivery.Id, broadcast.Options, chosen),
                cancellationToken: cancellationToken);
        }
        catch (ApiRequestException ex)
        {
            // deleted with /clear or not modified: the answer is stored anyway
            LogEditFailed(logger, delivery.Id, ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Broadcast {BroadcastId}: parent {ParentId} answered")]
    private static partial void LogAnswered(ILogger logger, Guid broadcastId, Guid parentId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Broadcast delivery {DeliveryId}: the message was not updated ({Reason})")]
    private static partial void LogEditFailed(ILogger logger, Guid deliveryId, string reason);
}
