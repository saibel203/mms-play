using MMS.Play.Contracts.Models.Notifications;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;

public interface ILessonReminderService
{
    /// <summary>
    /// Sends the lesson reminder to every active subscriber of the Telegram bot.
    /// Subscribers who blocked the bot are unsubscribed on the way.
    /// </summary>
    Task<LessonReminderResult> SendRemindersAsync(CancellationToken cancellationToken = default);
}
