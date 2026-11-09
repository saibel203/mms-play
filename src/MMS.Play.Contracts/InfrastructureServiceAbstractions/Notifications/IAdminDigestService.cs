using MMS.Play.Contracts.Exceptions;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;

/// <summary>
/// The daily summary for admins in the bot: today's lessons, cancellations, debts, open requests.
/// </summary>
public interface IAdminDigestService
{
    /// <summary>
    /// Called by the reminder job every minute: at the digest time (notification settings), sends the summary once a
    /// day to every active subscriber marked as admin. Does nothing at other times.
    /// </summary>
    Task SendDueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends today's summary to the admins right away (the "Send now" button), whatever the schedule says.
    /// Does not count as the day's digest.
    /// </summary>
    /// <returns>How many admins got it.</returns>
    /// <exception cref="ValidationFailedException">No active subscriber is an admin, or the bot is turned off.</exception>
    Task<int> SendNowAsync(CancellationToken cancellationToken = default);
}
