using System.Globalization;
using System.Net;
using MMS.Play.Contracts.Models.Notifications;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Infrastructure.Lessons;
using Telegram.Bot.Types.ReplyMarkups;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// How the bot talks about money: "1 500 грн", the lines of unpaid lessons and the filled in payment reminder.
/// </summary>
public static class PaymentTexts
{
    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");

    /// <summary>
    /// "500 грн", "1 500 грн", "450,50 грн".
    /// </summary>
    public static string Money(decimal amount) => $"{amount.ToString("#,0.##", Ukrainian)} грн";

    /// <summary>
    /// "• ср 12.11 о 14:00, Рома — 500 грн" per lesson; past <paramref name="maxLines"/> one "…і ще 3" line.
    /// </summary>
    public static string LessonLines(IReadOnlyList<DueLesson> lessons, int maxLines)
    {
        var lines = lessons
            .Take(maxLines)
            .Select(lesson => $"• {UkrainianDates.ShortDayTime(lesson.Date, lesson.StartTime)}, "
                              + $"{WebUtility.HtmlEncode(lesson.Teacher)} — {Money(lesson.Price)}")
            .ToList();

        if (lessons.Count > maxLines)
            lines.Add($"…і ще {lessons.Count - maxLines}");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Text of the «Оплатити» URL button under payment messages.
    /// </summary>
    public const string PayButtonText = "💳 Оплатити";

    /// <summary>
    /// The «Оплатити» button opening the payment link, null when no link is set.
    /// </summary>
    public static InlineKeyboardMarkup? PayButton(PaymentInfo payment) =>
        payment.Link is null ? null : new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl(PayButtonText, payment.Link));

    /// <summary>
    /// The link as a clickable &lt;a&gt;, empty when none is set.
    /// </summary>
    public static string LinkHtml(PaymentInfo payment) => payment.Link is null
        ? string.Empty
        : $"<a href=\"{WebUtility.HtmlEncode(payment.Link)}\">{WebUtility.HtmlEncode(payment.Link)}</a>";

    /// <summary>
    /// The payment details, HTML encoded (their line breaks stay), empty when none are set.
    /// </summary>
    public static string DetailsHtml(PaymentInfo payment) =>
        payment.Details is null ? string.Empty : WebUtility.HtmlEncode(payment.Details);

    /// <summary>
    /// Fills in a payment reminder; <paramref name="childName"/> is set for a parent's message.
    /// Without a link but with details that the template does not show itself, the details are added at the end,
    /// so a family always learns where to pay (with a link the «Оплатити» button does it).
    /// </summary>
    public static string Render(
        string template,
        string firstName,
        string? childName,
        IReadOnlyList<DueLesson> lessons,
        int maxLines,
        PaymentInfo payment)
    {
        var text = Fill(template, firstName, childName, lessons, maxLines, payment);

        return payment is { Link: null, Details: not null } && !template.Contains(MessagePlaceholders.PaymentDetails, StringComparison.Ordinal)
            ? $"{text}\n\n{DetailsBlock(payment)}"
            : text;
    }

    /// <summary>
    /// "Реквізити для оплати:" and the details; empty when none are set.
    /// </summary>
    public static string DetailsBlock(PaymentInfo payment) =>
        payment.Details is null ? string.Empty : $"🏦 <b>Реквізити для оплати:</b>\n{DetailsHtml(payment)}";

    private static string Fill(string template, string firstName, string? childName, IReadOnlyList<DueLesson> lessons, int maxLines, PaymentInfo payment) => template
        // the template is HTML, a name like "<Max>" would break the markup
        .Replace(MessagePlaceholders.FirstName, WebUtility.HtmlEncode(firstName), StringComparison.Ordinal)
        .Replace(MessagePlaceholders.ChildName, WebUtility.HtmlEncode(childName ?? string.Empty), StringComparison.Ordinal)
        .Replace(MessagePlaceholders.UnpaidCount, lessons.Count.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace(MessagePlaceholders.UnpaidAmount, Money(lessons.Sum(lesson => lesson.Price)), StringComparison.Ordinal)
        .Replace(MessagePlaceholders.UnpaidLessons, LessonLines(lessons, maxLines), StringComparison.Ordinal)
        .Replace(MessagePlaceholders.PaymentLink, LinkHtml(payment), StringComparison.Ordinal)
        .Replace(MessagePlaceholders.PaymentDetails, DetailsHtml(payment), StringComparison.Ordinal);
}

/// <summary>
/// Where to pay, from the lesson settings: a link (the «Оплатити» button) and/or plain text details.
/// </summary>
/// <param name="Link">Absolute http(s) URL, null when not set.</param>
/// <param name="Details">Recipient, IBAN, purpose; null when not set.</param>
public sealed record PaymentInfo(string? Link, string? Details)
{
    public bool IsEmpty => Link is null && Details is null;

    public static PaymentInfo From(NotificationSettingsDto settings) => new(settings.PaymentLink, settings.PaymentDetails);
}
