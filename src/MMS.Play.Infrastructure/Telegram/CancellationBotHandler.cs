using Microsoft.EntityFrameworkCore;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Infrastructure.Lessons;
using MMS.Play.Infrastructure.Persistence;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// Cancelling a lesson in the bot (see <see cref="CancellationView"/>): the button under a reminder or in /lessons
/// asks in a new message with the reasons; picking one cancels. The student of the lesson and the student's parents may.
/// </summary>
public class CancellationBotHandler(ApplicationDbContext context, LessonCancellationService cancellations, SchoolClock clock)
{
    public async Task HandleCallbackAsync(ITelegramBotClient botClient, CallbackQuery query, CancellationToken cancellationToken)
    {
        string? alert = null;
        try
        {
            if (query is { Data: { } data, Message: { } message })
                alert = await DispatchAsync(botClient, query.From, message, data, cancellationToken);
        }
        finally
        {
            // always answered, otherwise the button keeps spinning on the user's phone
            await botClient.AnswerCallbackQuery(query.Id, alert, showAlert: alert is not null, cancellationToken: cancellationToken);
        }
    }

    /// <returns>A hint shown as an alert, null when the message shows the result.</returns>
    private async Task<string?> DispatchAsync(ITelegramBotClient botClient, User user, Message message, string data, CancellationToken cancellationToken)
    {
        if (data == CancellationView.Keep)
        {
            await EditAsync(botClient, message, CancellationView.KeptText, cancellationToken);
            return null;
        }

        var (action, arguments) = RescheduleView.Parse(data);
        if (arguments.Length == 0 || !RescheduleView.TryParseId(arguments[0], out var lessonId))
            return null;

        var who = await cancellations.WhoMayCancelAsync(lessonId, user.Id, cancellationToken);
        if (who is not { } person)
            return CancellationView.ProblemText(CancellationProblem.NotFound);

        if (action == CancellationView.Ask)
        {
            var now = clock.Now;
            var lesson = await context.Lessons
                .AsNoTracking()
                .Where(x => x.Id == lessonId)
                .Select(x => new { x.Date, x.StartTime, x.Status, Room = x.Room.Name, Teacher = x.Teacher.Name, Student = x.Student!.FirstName })
                .FirstOrDefaultAsync(cancellationToken);

            if (lesson is null || lesson.Status != LessonStatus.Scheduled || lesson.Date.ToDateTime(lesson.StartTime) <= now)
                return CancellationView.ProblemText(CancellationProblem.NotCancellable);

            var isLate = lesson.Date.ToDateTime(lesson.StartTime) - now < TimeSpan.FromHours(LessonCancellationDto.LateHours);
            var view = CancellationView.AskView(
                lessonId, lesson.Date, lesson.StartTime, lesson.Room, lesson.Teacher,
                person.Source == CancellationSource.Parent ? lesson.Student : null,
                isLate, clock.Today);

            // the reminder or the lesson list stays as it is, the question gets its own message
            await botClient.SendMessage(message.Chat.Id, view.Text, ParseMode.Html, replyMarkup: view.Keyboard, cancellationToken: cancellationToken);
            return null;
        }

        if (action == CancellationView.Confirm
            && arguments.Length == 2
            && int.TryParse(arguments[1], out var reasonIndex)
            && reasonIndex >= 0 && reasonIndex < CancellationView.Reasons.Count)
        {
            var (cancelled, problem) = await cancellations.CancelAsync(
                lessonId, person.Source, person.Name, CancellationView.Reasons[reasonIndex], cancellationToken);

            if (cancelled is null)
            {
                await EditAsync(botClient, message, CancellationView.ProblemText(problem), cancellationToken);
                return null;
            }

            await EditAsync(botClient, message, CancellationView.CancelledText(cancelled, clock.Today), cancellationToken);
            return null;
        }

        return null;
    }

    private static async Task EditAsync(ITelegramBotClient botClient, Message message, string text, CancellationToken cancellationToken)
    {
        try
        {
            await botClient.EditMessageText(message.Chat.Id, message.Id, text, ParseMode.Html, cancellationToken: cancellationToken);
        }
        catch (ApiRequestException)
        {
            // pressed twice, or the message is gone (/clear)
        }
    }
}
