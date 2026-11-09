using MMS.Play.Contracts.Models.Common;
using MMS.Play.Contracts.Models.Notifications;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;

/// <summary>
/// Telegram bot subscribers as seen by an admin. Subscribing and unsubscribing is done by the users in the bot.
/// </summary>
public interface ITelegramSubscriberService
{
    /// <summary>
    /// Latest subscribers first.
    /// </summary>
    Task<PagedResult<TelegramSubscriberDto>> ListAsync(TelegramSubscriberListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns lesson reminders on or off for one subscriber. Kept when the user unsubscribes and subscribes again.
    /// </summary>
    /// <returns>The updated subscriber, null when it does not exist.</returns>
    Task<TelegramSubscriberDto?> SetRemindersEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes the subscriber a bot admin (gets reschedule requests and answers them in the bot) or takes it away.
    /// </summary>
    /// <returns>The updated subscriber, null when it does not exist.</returns>
    Task<TelegramSubscriberDto?> SetAdminAsync(Guid id, bool isAdmin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes the subscriber a parent (or not) and links their children: students, i.e. subscribers who are not parents themselves.
    /// The children are kept when the role is taken away.
    /// </summary>
    /// <returns>The updated subscriber, null when it does not exist.</returns>
    /// <exception cref="Exceptions.ValidationFailedException">An unknown child, the subscriber itself or a parent among the children.</exception>
    Task<TelegramSubscriberDto?> SetParentAsync(Guid id, bool isParent, IReadOnlyList<Guid> childIds, CancellationToken cancellationToken = default);
}
