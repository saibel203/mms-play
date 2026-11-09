using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Trial;

/// <summary>
/// Trial lesson messages in the bot. Without the bot nothing is sent: requests wait on the website.
/// A failed message is logged and skipped, the request itself is stored.
/// </summary>
public interface ITrialNotifier
{
    /// <summary>
    /// Every active admin subscriber gets the new request.
    /// </summary>
    Task NotifyAdminsAsync(TrialRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The bot requester learns when and where the lesson is, with a fresh menu (no trial button any more).
    /// </summary>
    Task NotifyBookedAsync(TrialRequest request, Lesson lesson, CancellationToken cancellationToken);

    /// <summary>
    /// The bot requester learns the request was turned down.
    /// </summary>
    Task NotifyDeclinedAsync(TrialRequest request, string? reason, CancellationToken cancellationToken);
}

/// <summary>
/// Used while the bot is turned off.
/// </summary>
public class NullTrialNotifier : ITrialNotifier
{
    public Task NotifyAdminsAsync(TrialRequest request, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task NotifyBookedAsync(TrialRequest request, Lesson lesson, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task NotifyDeclinedAsync(TrialRequest request, string? reason, CancellationToken cancellationToken) => Task.CompletedTask;
}
