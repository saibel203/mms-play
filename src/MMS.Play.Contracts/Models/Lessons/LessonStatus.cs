namespace MMS.Play.Contracts.Models.Lessons;

public enum LessonStatus
{
    /// <summary>
    /// Takes place as planned.
    /// </summary>
    Scheduled,

    /// <summary>
    /// Moved to another time (see <see cref="LessonDto.RescheduledToId"/>). Kept to show where the lesson was,
    /// left out of counts, earnings, reminders and clash checks.
    /// </summary>
    Rescheduled,

    /// <summary>
    /// Will not take place (see <see cref="LessonDto.Cancellation"/>). Kept, crossed out on the website, and left out
    /// of counts, earnings, debts, reminders and clash checks; an admin can restore it.
    /// </summary>
    Cancelled
}
