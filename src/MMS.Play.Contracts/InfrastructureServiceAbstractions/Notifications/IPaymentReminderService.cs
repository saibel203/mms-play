using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.Models.Notifications;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;

/// <summary>
/// Reminds students and their parents in the bot of lessons that took place and are not paid.
/// </summary>
public interface IPaymentReminderService
{
    /// <summary>
    /// Called by the reminder job: at the payment reminder time, reminds everybody who owes and was not
    /// reminded within the last "every N days". Does nothing at other times.
    /// </summary>
    Task<LessonReminderResult> SendDueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reminds one student and their parents right away, whenever they were reminded last.
    /// </summary>
    /// <exception cref="ValidationFailedException">The student owes nothing, nobody can get the message, or the bot is turned off.</exception>
    Task<LessonReminderResult> RemindStudentAsync(Guid studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reminds every student who owes, and their parents, right away, whenever they were reminded last
    /// (the "Remind everyone" button). Counts as the latest reminder, like <see cref="RemindStudentAsync"/>.
    /// </summary>
    /// <exception cref="ValidationFailedException">Nobody owes, or the bot is turned off.</exception>
    Task<PaymentRemindAllResult> RemindAllAsync(CancellationToken cancellationToken = default);
}
