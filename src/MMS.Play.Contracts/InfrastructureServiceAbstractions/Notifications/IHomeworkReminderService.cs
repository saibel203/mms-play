using MMS.Play.Contracts.Models.Notifications;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;

/// <summary>
/// The daily «time to practise» message in the bot to students with active homework.
/// </summary>
public interface IHomeworkReminderService
{
    /// <summary>
    /// Called by the reminder job every minute: at the homework reminder time (notification settings), reminds once a
    /// day every student with active homework who has not marked it practised today. Does nothing at other times.
    /// </summary>
    Task<LessonReminderResult> SendDueAsync(CancellationToken cancellationToken = default);
}
