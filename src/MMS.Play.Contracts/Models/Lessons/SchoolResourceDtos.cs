namespace MMS.Play.Contracts.Models.Lessons;

/// <summary>
/// A teacher on the "teachers and rooms" page.
/// </summary>
/// <param name="Id">Teacher identifier.</param>
/// <param name="Name">First name, shown on the timetable.</param>
/// <param name="Color">CSS hex color, e.g. "#22c55e".</param>
/// <param name="LessonCount">Lessons of this teacher, rescheduled ones included; a teacher with lessons cannot be deleted.</param>
public record TeacherSummaryDto(Guid Id, string Name, string Color, int LessonCount)
{
    public const int NameMaxLength = 64;
}

/// <summary>
/// A new or changed teacher.
/// </summary>
/// <param name="Name">Unique, up to 64 characters.</param>
/// <param name="Color">"#rrggbb".</param>
public record TeacherInput(string Name, string Color);

/// <summary>
/// A room on the "teachers and rooms" page.
/// </summary>
/// <param name="Id">Room identifier.</param>
/// <param name="Name">Room number, e.g. "314".</param>
/// <param name="Hours">Days the room is open, Monday first; a day without an entry is closed.</param>
/// <param name="LessonCount">Lessons in this room, rescheduled ones included; a room with lessons cannot be deleted.</param>
public record RoomSummaryDto(Guid Id, string Name, IReadOnlyList<RoomHoursDto> Hours, int LessonCount)
{
    public const int NameMaxLength = 32;
}

/// <summary>
/// A new or changed room.
/// </summary>
/// <param name="Name">Unique, up to 32 characters.</param>
/// <param name="Hours">At most one entry per day; first lesson not after the last one, both between 07:00 and 22:00.</param>
public record RoomInput(string Name, IReadOnlyList<RoomHoursDto> Hours);
