using MMS.Play.Infrastructure.Lessons;
using MMS.Play.Infrastructure.Trial;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// /trial and «🎸 Пробний урок»: a newcomer picks a day and a time (or "any time") and sends a trial lesson request,
/// admins get it in the bot. The steps edit the same message; see <see cref="TrialView"/>. Who may ask is decided
/// by <see cref="TrialEligibility"/>, also on every button, since the buttons may be old.
/// </summary>
public class TrialBotHandler(TrialBookingService trialService, TrialEligibility eligibility, SchoolClock clock)
{
    public async Task HandleCommandAsync(ITelegramBotClient botClient, long chatId, long telegramUserId, CancellationToken cancellationToken)
    {
        var result = await eligibility.CheckAsync(telegramUserId, cancellationToken);
        if (result != TrialEligibility.Result.Eligible)
        {
            await botClient.SendMessage(
                chatId,
                TrialEligibility.Explain(result),
                ParseMode.Html,
                replyMarkup: BotMenu.KeyboardFor(false),
                cancellationToken: cancellationToken);
            return;
        }

        var view = TrialView.DaysView(await trialService.FindSlotsAsync(cancellationToken), trialService.DaysAhead);
        await botClient.SendMessage(chatId, view.Text, ParseMode.Html, replyMarkup: view.Keyboard, cancellationToken: cancellationToken);
    }

    public async Task HandleCallbackAsync(ITelegramBotClient botClient, CallbackQuery query, CancellationToken cancellationToken)
    {
        string? answer = null;

        try
        {
            answer = await DispatchAsync(botClient, query, cancellationToken);
        }
        finally
        {
            // always answered, otherwise the button keeps spinning on the user's phone
            await botClient.AnswerCallbackQuery(query.Id, answer, showAlert: answer is not null, cancellationToken: cancellationToken);
        }
    }

    /// <returns>A hint shown as an alert, null when the message itself shows the result.</returns>
    private async Task<string?> DispatchAsync(ITelegramBotClient botClient, CallbackQuery query, CancellationToken cancellationToken)
    {
        if (query.Data is null || query.Message is not { } message)
            return null;

        var action = TrialView.Parse(query.Data);
        if (action is TrialView.Dismiss)
        {
            await EditAsync(botClient, message, new TrialView.View(TrialView.ClosedText, null), cancellationToken);
            return null;
        }

        if (action is null)
            return null;

        var result = await eligibility.CheckAsync(query.From.Id, cancellationToken);
        if (result != TrialEligibility.Result.Eligible)
        {
            await EditAsync(botClient, message, new TrialView.View(TrialEligibility.Explain(result), null), cancellationToken);
            return null;
        }

        switch (action)
        {
            case TrialView.ShowDays:
                await EditAsync(botClient, message,
                    TrialView.DaysView(await trialService.FindSlotsAsync(cancellationToken), trialService.DaysAhead), cancellationToken);
                return null;

            case TrialView.ShowDay(var date):
                await EditAsync(botClient, message, TrialView.HoursView(await trialService.FindSlotsAsync(cancellationToken), date), cancellationToken);
                return null;

            case TrialView.AskConfirm(var date, var time):
                await EditAsync(botClient, message, TrialView.ConfirmView(date, time, clock.Today), cancellationToken);
                return null;

            case TrialView.Send(var date, var time):
            {
                var (outcome, _) = await trialService.CreateFromBotAsync(query.From.Id, date, time, cancellationToken);
                switch (outcome)
                {
                    case TrialBookingService.BotOutcome.SlotTaken:
                        await EditAsync(botClient, message,
                            TrialView.DaysView(await trialService.FindSlotsAsync(cancellationToken), trialService.DaysAhead), cancellationToken);
                        return TrialView.SlotTakenText;

                    case TrialBookingService.BotOutcome.NotEligible:
                        var again = await eligibility.CheckAsync(query.From.Id, cancellationToken);
                        await EditAsync(botClient, message, new TrialView.View(TrialEligibility.Explain(again), null), cancellationToken);
                        return null;
                }

                await EditAsync(botClient, message, new TrialView.View(TrialView.SentText(date, time, clock.Today), null), cancellationToken);
                // only a new message changes the menu: the trial button goes away now that a request waits
                await botClient.SendMessage(
                    message.Chat.Id,
                    "Поки чекаєте, можна подивитися, що вміє бот: «❓ Допомога».",
                    replyMarkup: BotMenu.KeyboardFor(false),
                    cancellationToken: cancellationToken);
                return null;
            }

            default:
                return null;
        }
    }

    private static async Task EditAsync(ITelegramBotClient botClient, Message message, TrialView.View view, CancellationToken cancellationToken)
    {
        try
        {
            await botClient.EditMessageText(message.Chat.Id, message.Id, view.Text, ParseMode.Html, replyMarkup: view.Keyboard, cancellationToken: cancellationToken);
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
        {
            // the same button pressed twice
        }
    }
}
