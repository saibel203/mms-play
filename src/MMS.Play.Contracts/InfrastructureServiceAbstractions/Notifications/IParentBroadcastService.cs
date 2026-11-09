using MMS.Play.Contracts.Models.Common;
using MMS.Play.Contracts.Models.Notifications;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;

/// <summary>
/// Messages and questions an admin sends to parents in the Telegram bot, and the parents' answers.
/// </summary>
public interface IParentBroadcastService
{
    /// <summary>
    /// Who would get a broadcast, for the admin to check and uncheck before sending.
    /// </summary>
    /// <param name="audience">Every parent, or the parents of the students with a lesson on <paramref name="lessonDate"/>.</param>
    /// <param name="lessonDate">Required with <see cref="BroadcastAudience.LessonDay"/>.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    Task<IReadOnlyList<BroadcastRecipientDto>> ListRecipientsAsync(BroadcastAudience audience, DateOnly? lessonDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the broadcast and sends it right away, one message per recipient.
    /// </summary>
    /// <param name="input">The broadcast.</param>
    /// <param name="createdBy">The admin's user name.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <exception cref="Exceptions.ValidationFailedException">Invalid text or options, unknown recipients, or the bot is turned off.</exception>
    Task<BroadcastDetailsDto> SendAsync(BroadcastInput input, string? createdBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sent broadcasts, latest first.
    /// </summary>
    Task<PagedResult<BroadcastSummaryDto>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <returns>Null when it does not exist.</returns>
    Task<BroadcastDetailsDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}
