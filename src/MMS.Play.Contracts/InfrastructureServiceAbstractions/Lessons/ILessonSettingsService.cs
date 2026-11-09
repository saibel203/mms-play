using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.Models.Lessons;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Lessons;

/// <summary>
/// The default lesson price: saved by an admin, the appsettings default until then.
/// The bot's schedule is in <see cref="Notifications.INotificationSettingsService"/>.
/// </summary>
public interface ILessonSettingsService
{
    Task<LessonSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    /// <exception cref="ValidationFailedException">The price is out of range.</exception>
    Task<LessonSettingsDto> UpdateAsync(LessonSettingsUpdate update, CancellationToken cancellationToken = default);
}
