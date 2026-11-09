using Microsoft.Extensions.Logging;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Infrastructure.Lessons;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// The reschedule flow in the bot: the student's buttons (start, day, time, close, withdraw), the admins'
/// buttons (yes / no) and /lessons. Steps of the student's flow edit the same message;
/// see <see cref="RescheduleView"/> for the callback data.
/// </summary>
public partial class RescheduleBotHandler(
    LessonRescheduleService rescheduleService,
    SchoolClock clock,
    ILogger<RescheduleBotHandler> logger)
{
    private const int UpcomingLessonCount = 5;

    public async Task HandleLessonsCommandAsync(ITelegramBotClient botClient, long chatId, User from, CancellationToken cancellationToken)
    {
        var lessons = await rescheduleService.ListUpcomingAsync(from.Id, UpcomingLessonCount, cancellationToken);
        var view = RescheduleView.LessonsView(lessons, clock.Today);

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

    /// <returns>A hint shown to the user as an alert, null when the message itself shows the result.</returns>
    private async Task<string?> DispatchAsync(ITelegramBotClient botClient, CallbackQuery query, CancellationToken cancellationToken)
    {
        if (query.Data is null || query.Message is not { } message)
            return null;

        var (action, arguments) = RescheduleView.Parse(query.Data);
        var userId = query.From.Id;
        var chatId = message.Chat.Id;
        var today = clock.Today;

        switch (action)
        {
            case RescheduleView.Start or RescheduleView.Days when arguments.Length == 1 && RescheduleView.TryParseId(arguments[0], out var lessonId):
            {
                var (options, problem) = await rescheduleService.GetOptionsAsync(lessonId, userId, cancellationToken);
                if (options is null)
                    return ProblemText(problem);

                var view = RescheduleView.DaysView(options, today);
                if (action == RescheduleView.Start)
                    // a reminder or the lesson list stays as it is, the flow gets its own message
                    await botClient.SendMessage(chatId, view.Text, ParseMode.Html, replyMarkup: view.Keyboard, cancellationToken: cancellationToken);
                else
                    await EditAsync(botClient, message, view, cancellationToken);
                return null;
            }

            case RescheduleView.Day when arguments.Length == 2
                                         && RescheduleView.TryParseId(arguments[0], out var lessonId)
                                         && RescheduleView.TryParseDate(arguments[1], out var date):
            {
                var (options, problem) = await rescheduleService.GetOptionsAsync(lessonId, userId, cancellationToken);
                if (options is null)
                    return ProblemText(problem);

                await EditAsync(botClient, message, RescheduleView.HoursView(options, date, today), cancellationToken);
                return null;
            }

            case RescheduleView.Time when arguments.Length == 2
                                          && RescheduleView.TryParseId(arguments[0], out var lessonId)
                                          && RescheduleView.TryParseDateTime(arguments[1], out var date, out var time):
            {
                var (request, problem) = await rescheduleService.ProposeAsync(lessonId, userId, chatId, date, time, cancellationToken);
                if (request is not null)
                {
                    await EditAsync(botClient, message, RescheduleView.PendingView(request, today), cancellationToken);
                    return null;
                }

                if (problem != RescheduleProblem.SlotTaken)
                    return ProblemText(problem);

                // taken in the meantime: show what is free now
                var (options, _) = await rescheduleService.GetOptionsAsync(lessonId, userId, cancellationToken);
                if (options is not null)
                    await EditAsync(botClient, message, RescheduleView.DaysView(options, today, "⚠️ Цей час щойно зайняли, оберіть інший."), cancellationToken);
                return ProblemText(problem);
            }

            case RescheduleView.Close:
                await EditAsync(botClient, message, new RescheduleView.View("Добре, урок лишається без змін.", null), cancellationToken);
                return null;

            case RescheduleView.Withdraw when arguments.Length == 1 && RescheduleView.TryParseId(arguments[0], out var requestId):
                if (!await rescheduleService.WithdrawAsync(requestId, userId, cancellationToken))
                    return "Запит уже розглянуто, відповідь має бути вище.";

                await EditAsync(botClient, message, new RescheduleView.View(RescheduleView.WithdrawnText(null), null), cancellationToken);
                return null;

            case RescheduleView.Approve or RescheduleView.Decline when arguments.Length == 1 && RescheduleView.TryParseId(arguments[0], out var requestId):
                return await DecideAsync(botClient, message, query.From, requestId, approve: action == RescheduleView.Approve, cancellationToken);

            case RescheduleView.Declined:
                return "Цей час адміністратор уже відхилив, оберіть інший.";

            default:
                LogUnknownCallback(logger, query.Data);
                return null;
        }
    }

    private async Task<string?> DecideAsync(
        ITelegramBotClient botClient,
        Message message,
        User admin,
        Guid requestId,
        bool approve,
        CancellationToken cancellationToken)
    {
        if (!await rescheduleService.IsAdminAsync(admin.Id, cancellationToken))
            return "Відповідати на запити можуть лише адміністратори.";

        var name = admin.Username is { } username ? $"{admin.FirstName} (@{username})" : admin.FirstName;
        var decision = await rescheduleService.DecideAsync(requestId, approve, name, cancellationToken);

        switch (decision)
        {
            case RescheduleDecision.AlreadyDecided or RescheduleDecision.NotFound:
                // someone else answered or the student withdrew: this copy just loses its buttons
                await TryEditKeyboardAsync(botClient, message, cancellationToken);
                return "Цей запит уже розглянуто.";
            case RescheduleDecision.SlotTaken:
                return "Цей час уже зайнятий, учня попрошено обрати інший.";
            default:
                // the request messages of every admin, this one included, were updated by the notifier
                return null;
        }
    }

    private async Task EditAsync(ITelegramBotClient botClient, Message message, RescheduleView.View view, CancellationToken cancellationToken)
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

    private static async Task TryEditKeyboardAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        try
        {
            await botClient.EditMessageReplyMarkup(message.Chat.Id, message.Id, (InlineKeyboardMarkup?)null, cancellationToken: cancellationToken);
        }
        catch (ApiRequestException)
        {
            // already without buttons
        }
    }

    private static string ProblemText(RescheduleProblem? problem) => problem switch
    {
        RescheduleProblem.NotMovable => "Цей урок уже не можна перенести.",
        RescheduleProblem.AlreadyPending => "Запит на перенесення цього уроку вже чекає підтвердження.",
        RescheduleProblem.SlotTaken => "Цей час уже зайнятий.",
        _ => "Урок не знайдено, можливо, його змінили. Надішліть /lessons, щоб побачити свої уроки."
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unknown callback data {Data}")]
    private static partial void LogUnknownCallback(ILogger logger, string data);
}
