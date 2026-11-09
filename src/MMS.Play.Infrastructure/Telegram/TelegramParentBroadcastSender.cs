using System.Net;
using Microsoft.Extensions.Logging;
using MMS.Play.Infrastructure.Notifications;
using MMS.Play.Infrastructure.Persistence.Entities;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

namespace MMS.Play.Infrastructure.Telegram;

public partial class TelegramParentBroadcastSender(ITelegramBotClient botClient, ILogger<TelegramParentBroadcastSender> logger)
    : IParentBroadcastSender
{
    public bool IsAvailable => true;

    public async Task<BroadcastSendResult> SendAsync(ParentBroadcast broadcast, ParentBroadcastDelivery delivery, CancellationToken cancellationToken)
    {
        var text = ParentBroadcastView.Text(delivery.Text, broadcast.Options, broadcast.AllowTextAnswer, answer: null);
        var markup = ParentBroadcastView.NewMessageMarkup(delivery.Id, broadcast.Options, broadcast.AllowTextAnswer);

        // one retry is enough: Telegram says exactly how long to wait
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var message = await botClient.SendMessage(delivery.ChatId, text, ParseMode.Html, replyMarkup: markup, cancellationToken: cancellationToken);
                return new BroadcastSendResult(message.Id, null);
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == (int)HttpStatusCode.TooManyRequests && attempt == 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(ex.Parameters?.RetryAfter ?? 1), cancellationToken);
            }
            catch (ApiRequestException ex)
            {
                // 403: blocked the bot or deleted the account; 400 "chat not found": the chat is gone
                var unreachable = ex.ErrorCode == (int)HttpStatusCode.Forbidden
                                  || (ex.ErrorCode == (int)HttpStatusCode.BadRequest
                                      && ex.Message.Contains("chat not found", StringComparison.OrdinalIgnoreCase));
                LogSendFailed(logger, delivery.ParentId, ex.Message);
                return new BroadcastSendResult(null, ex.Message, unreachable);
            }
            catch (Exception ex) when (ex is RequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                LogSendFailed(logger, delivery.ParentId, ex.Message);
                return new BroadcastSendResult(null, "Telegram did not answer, try again later.");
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Broadcast to parent {ParentId} not sent: {Reason}")]
    private static partial void LogSendFailed(ILogger logger, Guid parentId, string reason);
}
