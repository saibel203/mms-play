using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MMS.Play.Contracts.Models.Notifications;
using MMS.Play.Infrastructure.Homeworks;
using Telegram.Bot.Types.ReplyMarkups;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// How the bot talks about homework: the description of one, the filled in templates, the «✅ Позаймався» buttons
/// (callback "hw:done:&lt;id&gt;") and the link to the practice page.
/// </summary>
public static partial class HomeworkTexts
{
    public const string CallbackPrefix = "hw:";
    private const string DoneCallbackPrefix = "hw:done:";

    public const string DoneButtonText = "✅ Позаймався";
    public const string PlayerButtonText = "🎧 Ноти й плеєр";

    // a button label has room for little, a long title is cut
    private const int ButtonTitleLength = 24;

    public static bool IsHomeworkCallback(string data) => data.StartsWith(CallbackPrefix, StringComparison.Ordinal);

    public static string DoneCallback(Guid homeworkId) => $"{DoneCallbackPrefix}{homeworkId:N}";

    /// <summary>
    /// The homework of a «Позаймався» button, null for any other data.
    /// </summary>
    public static Guid? ParseDoneCallback(string data) =>
        data.StartsWith(DoneCallbackPrefix, StringComparison.Ordinal) && Guid.TryParseExact(data[DoneCallbackPrefix.Length..], "N", out var id)
            ? id
            : null;

    /// <summary>
    /// "<b>Title</b>", the score when it is not the title, the bars, the tempo, the deadline and what to do; HTML.
    /// </summary>
    public static string Describe(HomeworkRow homework, bool withDueDate = true)
    {
        var text = new StringBuilder($"🎼 <b>{WebUtility.HtmlEncode(homework.Title)}</b>");

        if (homework.ScoreTitle is { } scoreTitle && !string.Equals(scoreTitle, homework.Title, StringComparison.OrdinalIgnoreCase))
        {
            text.Append($"\nНоти: «{WebUtility.HtmlEncode(scoreTitle)}»");
            if (homework.ScoreArtist is { } artist)
                text.Append($" — {WebUtility.HtmlEncode(artist)}");
        }

        var details = new List<string>();
        if (Bars(homework.FromBar, homework.ToBar) is { } bars)
            details.Add(bars);
        if (Tempo(homework.StartTempoPercent, homework.TargetTempoPercent) is { } tempo)
            details.Add(tempo);
        if (withDueDate && homework.DueDate is { } due)
            details.Add($"до {UkrainianDates.ShortDay(due)}");
        if (details.Count > 0)
            text.Append('\n').Append(string.Join(" · ", details));

        if (homework.Instructions is { } instructions)
            text.Append($"\n📝 {WebUtility.HtmlEncode(instructions)}");

        return text.ToString();
    }

    /// <summary>
    /// "такти 5–12", "з 5-го такту", "такти 1–12"; null for the whole piece.
    /// </summary>
    public static string? Bars(int? from, int? to) => (from, to) switch
    {
        (null, null) => null,
        ({ } first, null) => $"з {first}-го такту",
        (null, { } last) => $"такти 1–{last}",
        ({ } first, { } last) when first == last => $"такт {first}",
        ({ } first, { } last) => $"такти {first}–{last}"
    };

    /// <summary>
    /// "темп 60% → 100%", "темп 80%"; null when none is given.
    /// </summary>
    public static string? Tempo(int? start, int? target) => (start, target) switch
    {
        (null, null) => null,
        ({ } from, { } to) when from != to => $"темп {from}% → {to}%",
        _ => $"темп {start ?? target}%"
    };

    /// <summary>
    /// {DueDate}: "📅 Виконати до пт 14.11", empty without a deadline.
    /// </summary>
    public static string DueDateLine(DateOnly? dueDate) =>
        dueDate is { } due ? $"📅 Виконати до {UkrainianDates.ShortDay(due)}" : string.Empty;

    /// <summary>
    /// {PracticeLink}: a link to the practice page, empty without one. Where Telegram takes no link buttons
    /// (http, localhost) the address is written out, otherwise it is a short link text.
    /// </summary>
    public static string PracticeLinkHtml(string? url, bool canUseLinkButtons) => url switch
    {
        null => string.Empty,
        _ when canUseLinkButtons => $"🎧 <a href=\"{WebUtility.HtmlEncode(url)}\">Ноти й плеєр для занять</a>",
        _ => $"🎧 Ноти й плеєр для занять: {WebUtility.HtmlEncode(url)}"
    };

    /// <summary>
    /// Fills in a homework template. Placeholders left empty (no deadline, no link) would leave blank lines, those are squeezed.
    /// </summary>
    public static string Render(string template, string firstName, string homeworkHtml, string dueDateLine, int streak, string practiceLinkHtml)
    {
        var text = template
            // the template is HTML, a name like "<Max>" would break the markup
            .Replace(MessagePlaceholders.FirstName, WebUtility.HtmlEncode(firstName), StringComparison.Ordinal)
            .Replace(MessagePlaceholders.Homework, homeworkHtml, StringComparison.Ordinal)
            .Replace(MessagePlaceholders.DueDate, dueDateLine, StringComparison.Ordinal)
            .Replace(MessagePlaceholders.Streak, streak.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace(MessagePlaceholders.PracticeLink, practiceLinkHtml, StringComparison.Ordinal);

        return BlankLines().Replace(text, "\n\n").Trim();
    }

    /// <summary>
    /// One «✅ Позаймався» button per homework (with its title when there are several), and the practice page as a
    /// link button when Telegram takes it. Null when there is no button at all.
    /// </summary>
    public static InlineKeyboardMarkup? Keyboard(IReadOnlyList<HomeworkRow> toMark, string? practiceUrl, bool canUseLinkButtons)
    {
        var rows = toMark
            .Select(homework => new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    toMark.Count == 1 ? DoneButtonText : $"{DoneButtonText}: {Shorten(homework.Title)}",
                    DoneCallback(homework.Id))
            })
            .ToList();

        if (practiceUrl is not null && canUseLinkButtons)
            rows.Add([InlineKeyboardButton.WithUrl(PlayerButtonText, practiceUrl)]);

        return rows.Count == 0 ? null : new InlineKeyboardMarkup(rows);
    }

    /// <summary>
    /// "1 день", "3 дні", "5 днів".
    /// </summary>
    public static string Days(int count)
    {
        var lastTwo = count % 100;
        var last = count % 10;
        var word = lastTwo is >= 11 and <= 14 ? "днів"
            : last == 1 ? "день"
            : last is >= 2 and <= 4 ? "дні"
            : "днів";
        return $"{count} {word}";
    }

    private static string Shorten(string title) =>
        title.Length <= ButtonTitleLength ? title : $"{title[..(ButtonTitleLength - 1)].TrimEnd()}…";

    // three or more line breaks, possibly with spaces between them
    [GeneratedRegex(@"\n[ \t]*\n(?:[ \t]*\n)+")]
    private static partial Regex BlankLines();
}
