using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Lessons;

/// <summary>
/// Tells people in Telegram what happens to a reschedule request. Without the bot (Telegram:Enabled = false)
/// <see cref="NullRescheduleNotifier"/> does nothing and requests are answered on the website only.
/// </summary>
public interface IRescheduleNotifier
{
    /// <summary>
    /// Sends the request to every bot admin with "yes" / "no" buttons.
    /// </summary>
    /// <returns>The sent messages, to update them once the request is answered.</returns>
    Task<List<TelegramMessageRef>> NotifyAdminsAsync(RescheduleRequestInfo request, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the buttons of the admins' request messages with how it ended.
    /// </summary>
    Task UpdateAdminsAsync(IReadOnlyList<TelegramMessageRef> messages, RescheduleRequestInfo request, string outcome, CancellationToken cancellationToken);

    Task NotifyStudentApprovedAsync(RescheduleRequestInfo request, CancellationToken cancellationToken);

    /// <summary>
    /// Tells the student the time does not work and offers the free times again, the declined ones crossed out.
    /// </summary>
    Task NotifyStudentDeclinedAsync(RescheduleRequestInfo request, bool slotTaken, RescheduleOptions? options, CancellationToken cancellationToken);
}

/// <summary>
/// Used while the Telegram bot is turned off.
/// </summary>
public sealed class NullRescheduleNotifier : IRescheduleNotifier
{
    public Task<List<TelegramMessageRef>> NotifyAdminsAsync(RescheduleRequestInfo request, CancellationToken cancellationToken) =>
        Task.FromResult(new List<TelegramMessageRef>());

    public Task UpdateAdminsAsync(IReadOnlyList<TelegramMessageRef> messages, RescheduleRequestInfo request, string outcome, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task NotifyStudentApprovedAsync(RescheduleRequestInfo request, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task NotifyStudentDeclinedAsync(RescheduleRequestInfo request, bool slotTaken, RescheduleOptions? options, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
