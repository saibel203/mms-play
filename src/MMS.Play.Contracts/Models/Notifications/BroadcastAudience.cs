namespace MMS.Play.Contracts.Models.Notifications;

/// <summary>
/// Who a broadcast for parents goes to.
/// </summary>
public enum BroadcastAudience
{
    /// <summary>
    /// Every parent, one message each. Lesson placeholders are not available.
    /// </summary>
    AllParents,

    /// <summary>
    /// The parents of the students who have a lesson on a given day, one message per lesson.
    /// </summary>
    LessonDay
}
