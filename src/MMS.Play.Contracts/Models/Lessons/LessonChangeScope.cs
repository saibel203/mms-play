namespace MMS.Play.Contracts.Models.Lessons;

/// <summary>
/// Which lessons of a weekly series a change or a delete applies to. A lesson outside a series is always changed alone.
/// </summary>
public enum LessonChangeScope
{
    /// <summary>
    /// Only this lesson.
    /// </summary>
    This,

    /// <summary>
    /// This lesson and the later lessons of its series that have not started yet. A change moves them all by the same
    /// number of days and gives them the same time, room, teacher, student, note and price; their payment stays.
    /// Cancelled and rescheduled lessons are left as they are.
    /// </summary>
    Following
}
