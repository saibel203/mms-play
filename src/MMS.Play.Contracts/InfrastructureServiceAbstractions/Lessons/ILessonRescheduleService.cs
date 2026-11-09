using MMS.Play.Contracts.Models.Lessons;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Lessons;

/// <summary>
/// Lessons moved at a student's request: the student proposes a free time in the Telegram bot,
/// an admin approves or declines it in the bot or on the website.
/// </summary>
public interface ILessonRescheduleService
{
    /// <summary>
    /// Answers a pending request. Approving moves the lesson; either way the student is told in the bot
    /// and the admins' request messages are updated.
    /// </summary>
    /// <param name="requestId">Reschedule request identifier.</param>
    /// <param name="approve">True to move the lesson, false to ask the student for another time.</param>
    /// <param name="decidedBy">Who answered, shown to the other admins.</param>
    /// <param name="cancellationToken">Aborts the database work.</param>
    Task<RescheduleDecision> DecideAsync(Guid requestId, bool approve, string decidedBy, CancellationToken cancellationToken = default);
}
