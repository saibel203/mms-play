using System.Globalization;
using System.Text;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// Dates the way the bot talks to students: "завтра", "у середу (12.11)", "ср 12.11".
/// Written out by hand, .NET cultures have no accusative weekdays or short "ср".
/// </summary>
public static class UkrainianDates
{
    private static readonly string[] Short = ["нд", "пн", "вт", "ср", "чт", "пт", "сб"];

    // accusative, as in "урок у середу"
    private static readonly string[] OnDay = ["неділю", "понеділок", "вівторок", "середу", "четвер", "п'ятницю", "суботу"];

    /// <summary>
    /// "сьогодні", "завтра", otherwise "у середу (12.11)".
    /// </summary>
    public static string When(DateOnly date, DateOnly today) => (date.DayNumber - today.DayNumber) switch
    {
        0 => "сьогодні",
        1 => "завтра",
        _ => $"у {OnDay[(int)date.DayOfWeek]} ({DayMonth(date)})"
    };

    /// <summary>
    /// "на середу, 12.11", for "pick a time on ...".
    /// </summary>
    public static string ForDay(DateOnly date) => $"на {OnDay[(int)date.DayOfWeek]}, {DayMonth(date)}";

    /// <summary>
    /// "ср 12.11", short enough for a button.
    /// </summary>
    public static string ShortDay(DateOnly date) => $"{Short[(int)date.DayOfWeek]} {DayMonth(date)}";

    /// <summary>
    /// "ср 12.11 о 14:00".
    /// </summary>
    public static string ShortDayTime(DateOnly date, TimeOnly time) => $"{ShortDay(date)} о {Time(time)}";

    /// <summary>
    /// "1 вільна", "3 вільні", "6 вільних", "21 вільна": how many free hours a day has, for the day buttons.
    /// </summary>
    public static string FreeHours(int count)
    {
        var lastTwo = count % 100;
        var last = count % 10;
        var word = lastTwo is >= 11 and <= 14 ? "вільних"
            : last == 1 ? "вільна"
            : last is >= 2 and <= 4 ? "вільні"
            : "вільних";
        return $"{count} {word}";
    }

    public static string Time(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// "1̶4̶:̶0̶0̶": crossed out with combining characters, for button labels where HTML does not work.
    /// </summary>
    public static string Strike(string text)
    {
        var builder = new StringBuilder(text.Length * 2);
        foreach (var character in text)
            builder.Append(character).Append('̶');
        return builder.ToString();
    }

    private static string DayMonth(DateOnly date) => date.ToString("dd.MM", CultureInfo.InvariantCulture);
}
