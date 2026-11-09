namespace MMS.Play.Contracts.Models.Trial;

public enum TrialRequestStatus
{
    /// <summary>
    /// Waits for an admin.
    /// </summary>
    New,

    /// <summary>
    /// An admin created the lesson, see <see cref="TrialRequestDto.LessonId"/>.
    /// </summary>
    Booked,

    /// <summary>
    /// An admin turned it down.
    /// </summary>
    Declined
}
