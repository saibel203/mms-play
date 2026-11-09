using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.Models.Notifications;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;

/// <summary>
/// When and what the bot sends on its own (lesson, payment and homework reminders, the admin digest, trial lesson
/// booking): saved by an admin on the Telegram page, the appsettings defaults until then.
/// </summary>
public interface INotificationSettingsService
{
    Task<NotificationSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    /// <exception cref="ValidationFailedException">A value is out of range, a time list is empty or repeats, the link is not http(s).</exception>
    Task<NotificationSettingsDto> UpdateAsync(NotificationSettingsUpdate update, CancellationToken cancellationToken = default);
}
