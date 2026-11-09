namespace MMS.Play.Contracts.Models.Lessons;

/// <summary>
/// What happened to a reschedule request an admin answered.
/// </summary>
public enum RescheduleDecision
{
    /// <summary>
    /// The lesson was moved: the old one is marked as rescheduled, a new one takes the proposed time.
    /// </summary>
    Approved,

    /// <summary>
    /// The student was asked to pick another time.
    /// </summary>
    Declined,

    /// <summary>
    /// The proposed time got taken in the meantime; the request was declined and the student asked to pick another time.
    /// </summary>
    SlotTaken,

    /// <summary>
    /// Someone else answered first, or the student cancelled it. Nothing changed.
    /// </summary>
    AlreadyDecided,

    NotFound
}
