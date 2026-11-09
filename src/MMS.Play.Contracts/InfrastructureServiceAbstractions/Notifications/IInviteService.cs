using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.Models.Notifications;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;

/// <summary>
/// Invite links: a person opens one in Telegram, presses Start and is registered with the invite's role.
/// </summary>
public interface IInviteService
{
    /// <summary>
    /// Latest first.
    /// </summary>
    Task<IReadOnlyList<InviteDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <exception cref="ValidationFailedException">Children for a student, unknown children or a parent among them, limits out of range.</exception>
    Task<InviteDto> CreateAsync(InviteInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// The link stops working; who used it keeps their role.
    /// </summary>
    /// <returns>False when it does not exist.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
