namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// The time slots of a room on one day of the week: one an hour from <see cref="FirstLessonAt"/> to <see cref="LastLessonAt"/>.
/// A day without a row is closed. Lessons outside the slots are still allowed and shown.
/// </summary>
public class RoomHours
{
    public Guid RoomId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>
    /// Start of the first lesson of the day, school time.
    /// </summary>
    public TimeOnly FirstLessonAt { get; set; }

    /// <summary>
    /// Start of the last lesson of the day, school time.
    /// </summary>
    public TimeOnly LastLessonAt { get; set; }
}
