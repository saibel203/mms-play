namespace MMS.Play.Contracts.Models.Lessons;

/// <summary>
/// Everything the timetable of one week needs: rooms with their hours, teachers and the lessons.
/// </summary>
/// <param name="WeekStart">Monday.</param>
/// <param name="WeekEnd">Sunday.</param>
/// <param name="Today">Today in the school's time zone.</param>
/// <param name="Rooms">In display order.</param>
/// <param name="Teachers">In display order.</param>
/// <param name="Lessons">Lessons of the week by date and time.</param>
public record WeekScheduleDto(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    DateOnly Today,
    IReadOnlyList<RoomDto> Rooms,
    IReadOnlyList<TeacherDto> Teachers,
    IReadOnlyList<LessonDto> Lessons);

/// <param name="Id">Room identifier.</param>
/// <param name="Name">Room number, e.g. "314".</param>
/// <param name="Hours">Days the room is open, a day without an entry is closed.</param>
public record RoomDto(Guid Id, string Name, IReadOnlyList<RoomHoursDto> Hours);

/// <summary>
/// Time slots of a room on a day of the week: one per hour from <paramref name="FirstLessonAt"/> to <paramref name="LastLessonAt"/>.
/// </summary>
public record RoomHoursDto(DayOfWeek DayOfWeek, TimeOnly FirstLessonAt, TimeOnly LastLessonAt);

/// <param name="Id">Teacher identifier.</param>
/// <param name="Name">First name.</param>
/// <param name="Color">CSS hex color the teacher's lessons are shown with, e.g. "#22c55e".</param>
public record TeacherDto(Guid Id, string Name, string Color);

/// <summary>
/// A Telegram subscriber offered as the student of a lesson.
/// </summary>
/// <param name="Id">Subscriber identifier.</param>
/// <param name="Name">First and last name.</param>
/// <param name="Username">Telegram username without "@".</param>
/// <param name="IsSubscribed">False when the user unsubscribed in the bot: lessons are possible, reminders are not.</param>
public record StudentOptionDto(Guid Id, string Name, string? Username, bool IsSubscribed);
