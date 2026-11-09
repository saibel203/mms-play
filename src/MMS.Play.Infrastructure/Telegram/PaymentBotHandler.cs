using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;
using MMS.Play.Infrastructure.Lessons;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Trial;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// /payments: the lessons that took place and are not paid. /pay: how much to pay and where.
/// A student sees their own, a parent those of each linked child. Where to pay comes from the lesson settings:
/// a link becomes the «Оплатити» button, the details are written out.
/// </summary>
public class PaymentBotHandler(
    ApplicationDbContext context,
    SchoolClock clock,
    INotificationSettingsService settingsService,
    TrialEligibility trialEligibility)
{
    // a chat message has room for plenty, but a long list is no longer readable
    private const int MaxLinesPerStudent = 20;

    private const string NoDetailsText = "Реквізити ще не додані, зверніться до адміністратора школи.";

    public async Task HandleAsync(ITelegramBotClient botClient, long chatId, long telegramUserId, CancellationToken cancellationToken)
    {
        var debt = await FindDebtAsync(telegramUserId, cancellationToken);
        if (debt.Problem is { } problem)
        {
            await botClient.SendMessage(chatId, problem, ParseMode.Html, replyMarkup: await trialEligibility.KeyboardAsync(telegramUserId, cancellationToken), cancellationToken: cancellationToken);
            return;
        }

        if (debt.Lessons.Count == 0)
        {
            var paid = debt.IsParent ? "✅ Усі уроки ваших дітей оплачено. Дякуємо!" : "✅ Усі уроки оплачено. Дякуємо!";
            await botClient.SendMessage(chatId, paid, ParseMode.Html, replyMarkup: await trialEligibility.KeyboardAsync(telegramUserId, cancellationToken), cancellationToken: cancellationToken);
            return;
        }

        var text = new StringBuilder("💳 <b>Неоплачені уроки</b>\n");
        foreach (var (id, name) in debt.Students)
        {
            var lessons = debt.Lessons.Where(lesson => lesson.StudentId == id).ToList();
            if (lessons.Count == 0)
                continue;

            text.Append('\n');
            if (name is not null)
                text.Append($"<b>{WebUtility.HtmlEncode(name)}</b>\n");
            text.Append(PaymentTexts.LessonLines(lessons, MaxLinesPerStudent)).Append('\n');
        }

        text.Append($"\nРазом: <b>{PaymentTexts.Money(debt.Total)}</b> за {debt.Lessons.Count} {LessonsWord(debt.Lessons.Count)}.");

        // with a link the button says where to pay, without one the details do
        var payment = PaymentInfo.From(await settingsService.GetAsync(cancellationToken));
        if (payment is { Link: null, Details: not null })
            text.Append("\n\n").Append(PaymentTexts.DetailsBlock(payment));

        await botClient.SendMessage(
            chatId, text.ToString(), ParseMode.Html, replyMarkup: PaymentTexts.PayButton(payment), cancellationToken: cancellationToken);
    }

    /// <summary>
    /// /pay and «💰 Оплатити»: the amount owed, the payment details and the «Оплатити» button. Works without a debt
    /// too (paying in advance), the amount line then says nothing is owed.
    /// </summary>
    public async Task HandlePayAsync(ITelegramBotClient botClient, long chatId, long telegramUserId, CancellationToken cancellationToken)
    {
        var debt = await FindDebtAsync(telegramUserId, cancellationToken);
        if (debt.Problem is { } problem)
        {
            await botClient.SendMessage(chatId, problem, ParseMode.Html, replyMarkup: await trialEligibility.KeyboardAsync(telegramUserId, cancellationToken), cancellationToken: cancellationToken);
            return;
        }

        var payment = PaymentInfo.From(await settingsService.GetAsync(cancellationToken));

        var text = new StringBuilder("💰 <b>Оплата уроків</b>\n\n");
        text.Append(debt.Lessons.Count == 0
            ? "Зараз боргів немає ✅"
            : $"До сплати: <b>{PaymentTexts.Money(debt.Total)}</b> за {debt.Lessons.Count} {LessonsWord(debt.Lessons.Count)}.");

        if (payment.IsEmpty)
        {
            text.Append("\n\n").Append(NoDetailsText);
        }
        else
        {
            if (payment.Details is not null)
                text.Append("\n\n").Append(PaymentTexts.DetailsBlock(payment));
            if (payment.Link is not null)
                text.Append("\n\nОплатити можна за кнопкою нижче 👇");
        }

        await botClient.SendMessage(
            chatId, text.ToString(), ParseMode.Html, replyMarkup: PaymentTexts.PayButton(payment), cancellationToken: cancellationToken);
    }

    /// <summary>
    /// The unpaid lessons of the user (a student) or of their children (a parent).
    /// </summary>
    private async Task<Debt> FindDebtAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var user = await context.TelegramSubscribers
            .AsNoTracking()
            .Where(subscriber => subscriber.TelegramUserId == telegramUserId)
            .Select(subscriber => new
            {
                subscriber.Id,
                subscriber.IsParent,
                Children = subscriber.Children.OrderBy(child => child.FirstName).Select(child => new { child.Id, child.FirstName }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return Debt.Failed("Спершу надішліть /start, щоб бот вас знав.");

        if (user.IsParent && user.Children.Count == 0)
            return Debt.Failed("До вашого профілю ще не прив'язано дітей. Зверніться до адміністратора школи.");

        List<(Guid Id, string? Name)> students = user.IsParent
            ? user.Children.Select(child => (child.Id, (string?)child.FirstName)).ToList()
            : [(user.Id, null)];

        var due = await DueLessons.FindAsync(context, students.Select(student => student.Id).ToList(), clock.Now, cancellationToken);
        return new Debt(user.IsParent, students, due, null);
    }

    private static string LessonsWord(int count)
    {
        var lastTwo = count % 100;
        var last = count % 10;
        return lastTwo is >= 11 and <= 14 ? "уроків"
            : last == 1 ? "урок"
            : last is >= 2 and <= 4 ? "уроки"
            : "уроків";
    }

    /// <param name="Students">The user, or their children with names (a parent).</param>
    /// <param name="Problem">Set when nothing can be shown: the reply instead.</param>
    private sealed record Debt(bool IsParent, List<(Guid Id, string? Name)> Students, List<DueLesson> Lessons, string? Problem)
    {
        public decimal Total => Lessons.Sum(lesson => lesson.Price);

        public static Debt Failed(string problem) => new(false, [], [], problem);
    }
}
