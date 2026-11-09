using System.Globalization;
using System.Net;
using MMS.Play.Contracts.Models.Trial;
using MMS.Play.Infrastructure.Persistence.Entities;
using Telegram.Bot.Types.ReplyMarkups;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// Texts and inline keyboards of the trial lesson flow in the bot: the days with free times, the times of a day,
/// a confirmation; plus the messages for admins and for the newcomer when an admin answers.
/// </summary>
public static class TrialView
{
    // Callback data is "trial:<action>[:<argument>]", at most 64 bytes.
    private const string Prefix = "trial:";
    /// <summary>trial:b: back to the days.</summary>
    private const string Days = "b";
    /// <summary>trial:d:{yyyyMMdd}: the free times of a day.</summary>
    private const string Day = "d";
    /// <summary>trial:t:{yyyyMMddHHmm}: confirm this time.</summary>
    private const string Time = "t";
    /// <summary>trial:c:{yyyyMMddHHmm}: send the request for this time.</summary>
    private const string Confirm = "c";
    /// <summary>trial:a: send the request without a time ("any time, call me").</summary>
    private const string AnyTime = "a";
    /// <summary>trial:x: close the flow.</summary>
    private const string Close = "x";

    private const string DateFormat = "yyyyMMdd";
    private const string DateTimeFormat = "yyyyMMddHHmm";

    public sealed record View(string Text, InlineKeyboardMarkup? Keyboard);

    /// <summary>
    /// A pressed button of this flow.
    /// </summary>
    public abstract record Action;
    public sealed record ShowDays : Action;
    public sealed record ShowDay(DateOnly Date) : Action;
    public sealed record AskConfirm(DateOnly Date, TimeOnly Time) : Action;
    public sealed record Send(DateOnly? Date, TimeOnly? Time) : Action;
    public sealed record Dismiss : Action;

    public static bool IsTrialCallback(string data) => data.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// The button pressed, null for malformed data.
    /// </summary>
    public static Action? Parse(string data)
    {
        var parts = data[Prefix.Length..].Split(':');
        return parts switch
        {
            [Days] => new ShowDays(),
            [Close] => new Dismiss(),
            [AnyTime] => new Send(null, null),
            [Day, var date] when TryParseDate(date, out var day) => new ShowDay(day),
            [Time, var value] when TryParseDateTime(value, out var moment) => new AskConfirm(moment.Date, moment.Time),
            [Confirm, var value] when TryParseDateTime(value, out var moment) => new Send(moment.Date, moment.Time),
            _ => null
        };
    }

    private const string Intro =
        "🎸 <b>Пробний урок</b>\n\n"
        + "Познайомимося, підберемо інструмент і програму під вас. Оберіть зручний день і час — "
        + "адміністратор підтвердить запис тут, у боті.";

    public static View DaysView(IReadOnlyList<TrialDayDto> days, int daysAhead)
    {
        var anyTimeRow = new[] { InlineKeyboardButton.WithCallbackData("📞 Будь-який час, зв'яжіться зі мною", Prefix + AnyTime) };
        var closeRow = new[] { InlineKeyboardButton.WithCallbackData("✖️ Закрити", Prefix + Close) };

        if (days.Count == 0)
            return new View(
                Intro + $"\n\nНа жаль, у найближчі {daysAhead} днів вільного часу немає. Залиште заявку — ми підберемо час разом.",
                new InlineKeyboardMarkup(new[] { anyTimeRow, closeRow }));

        var dayButtons = days.Select(day => InlineKeyboardButton.WithCallbackData(
            $"{UkrainianDates.ShortDay(day.Date)} — {UkrainianDates.FreeHours(day.Times.Count)}",
            $"{Prefix}{Day}:{day.Date.ToString(DateFormat, CultureInfo.InvariantCulture)}"));

        return new View(
            Intro + "\n\nОберіть день:",
            new InlineKeyboardMarkup(dayButtons.Chunk(2).Append(anyTimeRow).Append(closeRow)));
    }

    public static View HoursView(IReadOnlyList<TrialDayDto> days, DateOnly date)
    {
        var times = days.FirstOrDefault(day => day.Date == date)?.Times ?? [];
        var navigation = new[]
        {
            InlineKeyboardButton.WithCallbackData("« Інший день", Prefix + Days),
            InlineKeyboardButton.WithCallbackData("✖️ Закрити", Prefix + Close)
        };

        if (times.Count == 0)
            return new View(
                $"🎸 <b>Пробний урок</b>\n\n{Capitalize(UkrainianDates.ForDay(date))} вільного часу вже немає, оберіть інший день.",
                new InlineKeyboardMarkup(navigation));

        var timeButtons = times.Select(time => InlineKeyboardButton.WithCallbackData(
            UkrainianDates.Time(time),
            $"{Prefix}{Time}:{date.ToDateTime(time).ToString(DateTimeFormat, CultureInfo.InvariantCulture)}"));

        return new View(
            $"🎸 <b>Пробний урок</b>\n\nОберіть час {UkrainianDates.ForDay(date)}:",
            new InlineKeyboardMarkup(timeButtons.Chunk(4).Append(navigation)));
    }

    public static View ConfirmView(DateOnly date, TimeOnly time, DateOnly today)
    {
        var value = date.ToDateTime(time).ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        return new View(
            $"🎸 <b>Пробний урок</b>\n\nЗаписати вас {UkrainianDates.When(date, today)} о <b>{UkrainianDates.Time(time)}</b>?",
            new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("✅ Так, надіслати заявку", $"{Prefix}{Confirm}:{value}") },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("« Інший час", $"{Prefix}{Day}:{date.ToString(DateFormat, CultureInfo.InvariantCulture)}"),
                    InlineKeyboardButton.WithCallbackData("✖️ Закрити", Prefix + Close)
                }
            }));
    }

    public static string SentText(DateOnly? date, TimeOnly? time, DateOnly today) =>
        "⏳ <b>Заявку на пробний урок надіслано</b>\n\n"
        + (date is { } day && time is { } start
            ? $"Бажаний час: {UkrainianDates.When(day, today)} о {UkrainianDates.Time(start)}.\n"
            : "Час підберемо разом.\n")
        + "Адміністратор підтвердить запис, відповідь прийде сюди.";

    public const string ClosedText = "Добре! Якщо передумаєте — натисніть «" + BotMenu.TrialButton + "».";

    public const string SlotTakenText = "Цей час щойно зайняли 😔 Оберіть, будь ласка, інший.";

    /// <summary>
    /// For admins: who asked, when, how to reach them.
    /// </summary>
    public static string AdminText(TrialRequest request, DateOnly today) =>
        "🎸 <b>Нова заявка на пробний урок</b>\n\n"
        + $"Хто: <b>{Encode(request.Name)}</b>\n"
        + (request.Contact is null ? "" : $"Контакт: {Encode(request.Contact)}\n")
        + "Коли: " + (request is { PreferredDate: { } date, PreferredTime: { } time }
            ? $"{UkrainianDates.When(date, today)} о {UkrainianDates.Time(time)}\n"
            : "будь-який час, просить зв'язатися\n")
        + (request.Comment is null ? "" : $"Коментар: {Encode(request.Comment)}\n")
        + $"Звідки: {(request.Source == TrialRequestSource.Bot ? "бот" : "сайт")}\n\n"
        + "Записати чи відхилити — на сайті: Lessons → Trial requests.";

    public static string BookedText(Lesson lesson, DateOnly today) =>
        "✅ <b>Вас записано на пробний урок!</b>\n\n"
        + $"Коли: <b>{UkrainianDates.When(lesson.Date, today)} о {UkrainianDates.Time(lesson.StartTime)}</b>\n"
        + $"Де: каб. {Encode(lesson.Room.Name)}\n"
        + $"Викладач: {Encode(lesson.Teacher.Name)}\n\n"
        + "Перед уроком прийде нагадування. До зустрічі! 🎶";

    public static string DeclinedText(string? reason) =>
        "На жаль, цього разу записати вас на пробний урок не вийшло."
        + (reason is null ? "" : $"\n\n{Encode(reason)}")
        + "\n\nНапишіть адміністратору школи, і ми знайдемо рішення.";

    private static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static bool TryParseDateTime(string value, out (DateOnly Date, TimeOnly Time) moment)
    {
        if (DateTime.TryParseExact(value, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
        {
            moment = (DateOnly.FromDateTime(dateTime), TimeOnly.FromDateTime(dateTime));
            return true;
        }

        moment = default;
        return false;
    }

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static string Encode(string text) => WebUtility.HtmlEncode(text);
}
