using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// Receives updates with long polling (getUpdates): no public HTTPS address is needed, so it works on a dev machine.
/// Only one running instance may poll a bot, a second one gets 409 Conflict. A webhook can replace it later.
/// </summary>
public partial class TelegramPollingService(
    ITelegramBotClient botClient,
    TelegramUpdateHandler updateHandler,
    ILogger<TelegramPollingService> logger) : BackgroundService
{
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(30);

    private static readonly ReceiverOptions ReceiverOptions = new()
    {
        // everything else (edited messages, polls, groups...) is not handled yet, Telegram does not need to send it
        AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery, UpdateType.MyChatMember]
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // getUpdates fails with 409 Conflict while a webhook is set
                await botClient.DeleteWebhook(cancellationToken: stoppingToken);

                var me = await botClient.GetMe(stoppingToken);
                LogPollingStarted(logger, me.Username);
                await RegisterCommandsAsync(stoppingToken);

                // returns only on cancellation, errors go to the handler and polling goes on
                await botClient.ReceiveAsync(updateHandler, ReceiverOptions, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // an exception leaving ExecuteAsync stops the whole host, a broken bot must not take the API down
                LogPollingFailed(logger, ex, RestartDelay);

                try
                {
                    await Task.Delay(RestartDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// The command list behind the «Меню» button in Telegram. A failure only costs that list, the bot works without it.
    /// </summary>
    private async Task RegisterCommandsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await botClient.SetMyCommands(BotMenu.Commands, cancellationToken: cancellationToken);
        }
        catch (RequestException ex)
        {
            LogCommandsNotRegistered(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Telegram bot commands could not be registered")]
    private static partial void LogCommandsNotRegistered(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Telegram bot @{BotUsername} is receiving updates")]
    private static partial void LogPollingStarted(ILogger logger, string? botUsername);

    [LoggerMessage(Level = LogLevel.Error, Message = "Telegram bot could not start, retrying in {RestartDelay}")]
    private static partial void LogPollingFailed(ILogger logger, Exception exception, TimeSpan restartDelay);
}
