using System.Net;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Infrastructure.Lessons;
using Telegram.Bot.Types.ReplyMarkups;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// Texts and buttons of cancelling a lesson in the bot: ask (with a reason to pick), cancelled, kept,
/// and the messages to the admins and to the family.
/// </summary>
public static class CancellationView
{
    // Callback data is "<action>:<arguments>", ids as 32 hex digits ("N").
    /// <summary>xs:{lessonId}: ask in a new message.</summary>
    public const string Ask = "xs";
    /// <summary>xr:{lessonId}:{reasonIndex}: cancel with this reason.</summary>
    public const string Confirm = "xr";
    /// <summary>xq: keep the lesson.</summary>
    public const string Keep = "xq";

    /// <summary>
    /// The reasons offered in the bot, stored as written.
    /// </summary>
    public static readonly IReadOnlyList<string> Reasons = ["🤒 Хвороба", "📅 Інші плани", "🤷 Інша причина"];

    public static bool IsCancellationCallback(string data) =>
        data == Keep || data.StartsWith(Ask + ":", StringComparison.Ordinal) || data.StartsWith(Confirm + ":", StringComparison.Ordinal);

    public static InlineKeyboardButton AskButton(Guid lessonId, string label = "❌ Скасувати урок") =>
        InlineKeyboardButton.WithCallbackData(label, $"{Ask}:{lessonId:N}");

    public static RescheduleView.View AskView(Guid lessonId, DateOnly date, TimeOnly time, string roomName, string teacherName, string? childName, bool isLate, DateOnly today)
    {
        var text = "❌ <b>Скасувати урок?</b>\n\n"
                   + (childName is null ? "" : $"Учень: {Encode(childName)}\n")
                   + $"{UkrainianDates.When(date, today)} о {UkrainianDates.Time(time)}, каб. {Encode(roomName)}, викладач {Encode(teacherName)}\n"
                   + (isLate ? $"\n⚠️ До уроку менше {LessonCancellationDto.LateHours} годин — це пізнє скасування.\n" : "")
                   + "\nОберіть причину:";

        var rows = Reasons
            .Select((reason, index) => new[] { InlineKeyboardButton.WithCallbackData(reason, $"{Confirm}:{lessonId:N}:{index}") })
            .Append([InlineKeyboardButton.WithCallbackData("↩️ Не скасовувати", Keep)]);

        return new RescheduleView.View(text, new InlineKeyboardMarkup(rows));
    }

    public static string CancelledText(CancelledLessonInfo lesson, DateOnly today) =>
        $"✅ Урок {When(lesson, today)} скасовано. Адміністратора повідомлено.";

    public const string KeptText = "Добре, урок лишається.";

    public static string AdminText(CancelledLessonInfo lesson, DateOnly today)
    {
        var who = lesson.By switch
        {
            CancellationSource.Parent => $"батько/мати — {Encode(lesson.ByName ?? "")}",
            CancellationSource.Student => "учень",
            _ => "адміністратор"
        };

        return "❌ <b>Урок скасовано</b>\n\n"
               + $"Учень: {Encode(lesson.StudentName ?? "—")}\n"
               + $"Скасував: {who}\n"
               + $"Коли: <s>{When(lesson, today)}</s>, каб. {Encode(lesson.RoomName)}, викладач {Encode(lesson.TeacherName)}\n"
               + (lesson.Reason is { } reason ? $"Причина: {Encode(reason)}\n" : "")
               + (lesson.IsLate ? $"⚠️ Пізнє скасування (менше ніж за {LessonCancellationDto.LateHours} год)" : "");
    }

    /// <param name="lesson">The cancelled lesson.</param>
    /// <param name="childName">Set for a parent: whose lesson.</param>
    /// <param name="today">School date.</param>
    public static string FamilyText(CancelledLessonInfo lesson, string? childName, DateOnly today) =>
        "❌ <b>Урок скасовано</b>\n\n"
        + (childName is null ? "" : $"Учень: {Encode(childName)}\n")
        + $"<s>{When(lesson, today)}</s>, каб. {Encode(lesson.RoomName)}, викладач {Encode(lesson.TeacherName)}\n"
        + (lesson.Reason is { } reason ? $"Причина: {Encode(reason)}\n" : "")
        + "\nЯкщо це помилка, зверніться до адміністратора школи.";

    public static string ProblemText(CancellationProblem? problem) => problem switch
    {
        CancellationProblem.NotCancellable => "Цей урок уже не можна скасувати: він почався, уже скасований або перенесений.",
        _ => "Урок не знайдено, можливо, його змінили. Надішліть /lessons, щоб побачити свої уроки."
    };

    private static string When(CancelledLessonInfo lesson, DateOnly today) =>
        $"{UkrainianDates.When(lesson.Date, today)} о {UkrainianDates.Time(lesson.StartTime)}";

    private static string Encode(string text) => WebUtility.HtmlEncode(text);
}
