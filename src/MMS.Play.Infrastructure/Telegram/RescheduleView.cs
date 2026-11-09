using System.Globalization;
using System.Net;
using MMS.Play.Infrastructure.Lessons;
using Telegram.Bot.Types.ReplyMarkups;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// Texts and inline keyboards of the reschedule flow in the bot. A "calendar" is two steps of buttons:
/// the days with free times, then the times of the picked day. Times an admin declined stay on the
/// keyboard crossed out, so the student sees they were tried.
/// </summary>
public static class RescheduleView
{
    // Callback data is "<action>:<arguments>", at most 64 bytes: ids go as 32 hex digits ("N").
    /// <summary>rs:{lessonId}: start, a new message with the days.</summary>
    public const string Start = "rs";
    /// <summary>rb:{lessonId}: back to the days in the same message.</summary>
    public const string Days = "rb";
    /// <summary>rd:{lessonId}:{yyyyMMdd}: the free times of a day.</summary>
    public const string Day = "rd";
    /// <summary>rt:{lessonId}:{yyyyMMddHHmm}: propose this time.</summary>
    public const string Time = "rt";
    /// <summary>rq: close the flow without proposing.</summary>
    public const string Close = "rq";
    /// <summary>rc:{requestId}: the student withdraws the proposal.</summary>
    public const string Withdraw = "rc";
    /// <summary>ra:{requestId}: an admin approves.</summary>
    public const string Approve = "ra";
    /// <summary>rr:{requestId}: an admin declines.</summary>
    public const string Decline = "rr";
    /// <summary>rn: a crossed out (declined) time, only answers with a hint.</summary>
    public const string Declined = "rn";

    private const string DateFormat = "yyyyMMdd";
    private const string DateTimeFormat = "yyyyMMddHHmm";

    public sealed record View(string Text, InlineKeyboardMarkup? Keyboard);

    /// <summary>
    /// The button under every reminder.
    /// </summary>
    public static InlineKeyboardMarkup ReminderButtons(Guid lessonId) =>
        new(new[]
        {
            InlineKeyboardButton.WithCallbackData("🔁 Перенести урок", $"{Start}:{lessonId:N}"),
            CancellationView.AskButton(lessonId, "❌ Скасувати")
        });

    public static View DaysView(RescheduleOptions options, DateOnly today, string? intro = null)
    {
        var text = (intro is null ? "" : intro + "\n\n") + Header(options.Lesson, today) + DeclinedLine(options);
        var closeRow = new[] { InlineKeyboardButton.WithCallbackData("✖️ Закрити", Close) };

        if (options.Slots.Count == 0)
        {
            text += $"\n\nНа жаль, у найближчі {options.DaysAhead} днів у викладача {Encode(options.Lesson.TeacherName)} "
                    + "немає вільного часу. Напишіть адміністратору.";
            return new View(text, new InlineKeyboardMarkup(closeRow));
        }

        var dayButtons = options.Slots
            .GroupBy(slot => slot.Date)
            .Select(day => InlineKeyboardButton.WithCallbackData(
                $"{UkrainianDates.ShortDay(day.Key)} — {UkrainianDates.FreeHours(day.Count())}",
                $"{Day}:{options.Lesson.LessonId:N}:{day.Key.ToString(DateFormat, CultureInfo.InvariantCulture)}"));

        var rows = dayButtons.Chunk(2).Append(closeRow);
        return new View(text + "\n\nОберіть день, потім годину:", new InlineKeyboardMarkup(rows));
    }

    public static View HoursView(RescheduleOptions options, DateOnly date, DateOnly today)
    {
        var lessonId = options.Lesson.LessonId.ToString("N");
        var free = options.Slots.Where(slot => slot.Date == date).Select(slot => slot.Time).ToHashSet();
        var declined = options.Declined.Where(entry => entry.Date == date).Select(entry => entry.Time).ToHashSet();

        var timeButtons = free.Union(declined)
            .Order()
            .Select(time => declined.Contains(time)
                ? InlineKeyboardButton.WithCallbackData($"❌ {UkrainianDates.Strike(UkrainianDates.Time(time))}", Declined)
                : InlineKeyboardButton.WithCallbackData(
                    UkrainianDates.Time(time),
                    $"{Time}:{lessonId}:{date.ToDateTime(time).ToString(DateTimeFormat, CultureInfo.InvariantCulture)}"));

        var navigation = new[]
        {
            InlineKeyboardButton.WithCallbackData("« Інший день", $"{Days}:{lessonId}"),
            InlineKeyboardButton.WithCallbackData("✖️ Закрити", Close)
        };

        var text = Header(options.Lesson, today) + DeclinedLine(options) + (free.Count == 0
            ? $"\n\n{Capitalize(UkrainianDates.ForDay(date))} вільного часу вже немає, оберіть інший день."
            : $"\n\nОберіть час {UkrainianDates.ForDay(date)}:");

        return new View(text, new InlineKeyboardMarkup(timeButtons.Chunk(4).Append(navigation)));
    }

    public static View PendingView(RescheduleRequestInfo request, DateOnly today) => new(
        "⏳ <b>Запит надіслано</b>\n\n"
        + $"<s>{OldTime(request.Lesson)}</s> → <b>{NewTime(request)}</b>, каб. {Encode(request.ProposedRoomName)}\n\n"
        + "Чекаємо підтвердження адміністратора, відповідь прийде сюди.",
        new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Скасувати запит", $"{Withdraw}:{request.RequestId:N}")));

    public static string ClosedText(RescheduleLesson lesson) =>
        $"Перенесення скасовано. Урок лишається: {OldTime(lesson)}, каб. {Encode(lesson.RoomName)}.";

    public static string WithdrawnText(RescheduleRequestInfo? request) => request is null
        ? "Запит скасовано, урок лишається на своєму місці."
        : $"Запит скасовано. Урок лишається: {OldTime(request.Lesson)}.";

    public static View AdminRequestView(RescheduleRequestInfo request) => new(
        AdminText(request),
        new InlineKeyboardMarkup(new[]
        {
            InlineKeyboardButton.WithCallbackData("✅ Так", $"{Approve}:{request.RequestId:N}"),
            InlineKeyboardButton.WithCallbackData("❌ Ні", $"{Decline}:{request.RequestId:N}")
        }));

    public static string AdminOutcomeText(RescheduleRequestInfo request, string outcome) =>
        $"{AdminText(request)}\n\n<b>{Encode(outcome)}</b>";

    public static string ApprovedText(RescheduleRequestInfo request, DateOnly today) =>
        "✅ <b>Перенесення підтверджено!</b>\n\n"
        + $"Новий час: <b>{UkrainianDates.When(request.ProposedDate, today)} о {UkrainianDates.Time(request.ProposedTime)}</b>, "
        + $"каб. {Encode(request.ProposedRoomName)}, викладач {Encode(request.Lesson.TeacherName)}.";

    public static string DeclinedIntro(RescheduleRequestInfo request, bool slotTaken) => slotTaken
        ? $"⚠️ <s>{NewTime(request)}</s> уже зайнято, оберіть інший час."
        : $"❌ На жаль, <s>{NewTime(request)}</s> не підходить. Оберіть інший час.";

    /// <summary>
    /// /lessons: the next lessons, a reschedule and a cancel button each. A parent's children's lessons have the
    /// child's name and only the cancel button: moving a lesson is agreed by the student.
    /// </summary>
    public static View LessonsView(IReadOnlyList<UpcomingLesson> lessons, DateOnly today)
    {
        if (lessons.Count == 0)
            return new View("Найближчих уроків немає.", null);

        var lines = lessons.Select(lesson =>
            "• " + (lesson.ChildName is { } child ? $"<b>{Encode(child)}</b>: " : "")
            + $"{UkrainianDates.When(lesson.Date, today)} о {UkrainianDates.Time(lesson.StartTime)}, каб. {Encode(lesson.RoomName)}, "
            + $"{Encode(lesson.TeacherName)}{(lesson.HasPendingRequest ? " ⏳" : "")}");

        var buttons = lessons
            .Select(lesson =>
            {
                var when = UkrainianDates.ShortDayTime(lesson.Date, lesson.StartTime);
                var cancel = CancellationView.AskButton(lesson.LessonId, lesson.ChildName is null ? "❌ Скасувати" : $"❌ {lesson.ChildName}, {when}");
                return lesson.ChildName is null && !lesson.HasPendingRequest
                    ? new[] { InlineKeyboardButton.WithCallbackData($"🔁 Перенести {when}", $"{Start}:{lesson.LessonId:N}"), cancel }
                    : new[] { cancel };
            })
            .ToList();

        var heading = lessons.Any(lesson => lesson.ChildName is not null) ? "📅 <b>Найближчі уроки</b>" : "📅 <b>Ваші найближчі уроки</b>";
        var text = heading + "\n\n" + string.Join('\n', lines)
                   + (lessons.Any(lesson => lesson.HasPendingRequest) ? "\n\n⏳ — запит на перенесення чекає підтвердження." : "");

        return new View(text, new InlineKeyboardMarkup(buttons));
    }

    /// <summary>
    /// Splits "rt:abc:202611121400" into the action and its arguments.
    /// </summary>
    public static (string Action, string[] Arguments) Parse(string data)
    {
        var parts = data.Split(':');
        return (parts[0], parts[1..]);
    }

    public static bool TryParseId(string value, out Guid id) => Guid.TryParseExact(value, "N", out id);

    public static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static bool TryParseDateTime(string value, out DateOnly date, out TimeOnly time)
    {
        var parsed = DateTime.TryParseExact(value, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime);
        date = DateOnly.FromDateTime(dateTime);
        time = TimeOnly.FromDateTime(dateTime);
        return parsed;
    }

    private static string Header(RescheduleLesson lesson, DateOnly today) =>
        "🔁 <b>Перенесення уроку</b>\n"
        + $"Зараз: {UkrainianDates.When(lesson.Date, today)} о {UkrainianDates.Time(lesson.StartTime)}, "
        + $"каб. {Encode(lesson.RoomName)}, викладач {Encode(lesson.TeacherName)}";

    private static string DeclinedLine(RescheduleOptions options) => options.Declined.Count == 0
        ? ""
        : "\nНе підійшло: " + string.Join(", ", options.Declined
            .OrderBy(entry => entry.Date).ThenBy(entry => entry.Time)
            .Select(entry => $"<s>{UkrainianDates.ShortDayTime(entry.Date, entry.Time)}</s>"));

    private static string AdminText(RescheduleRequestInfo request)
    {
        var lesson = request.Lesson;
        var student = Encode(lesson.StudentName) + (lesson.StudentUsername is { } username ? $" (@{Encode(username)})" : "");
        return "🔁 <b>Запит на перенесення уроку</b>\n\n"
               + $"Учень: {student}\n"
               + $"Викладач: {Encode(lesson.TeacherName)}\n"
               + $"Було: <s>{OldTime(lesson)}, каб. {Encode(lesson.RoomName)}</s>\n"
               + $"Пропонує: <b>{NewTime(request)}, каб. {Encode(request.ProposedRoomName)}</b>\n\n"
               + "Підходить?";
    }

    private static string OldTime(RescheduleLesson lesson) => UkrainianDates.ShortDayTime(lesson.Date, lesson.StartTime);

    private static string NewTime(RescheduleRequestInfo request) => UkrainianDates.ShortDayTime(request.ProposedDate, request.ProposedTime);

    private static string Encode(string text) => WebUtility.HtmlEncode(text);

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
