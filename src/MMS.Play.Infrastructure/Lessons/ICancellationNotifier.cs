namespace MMS.Play.Infrastructure.Lessons;

/// <summary>
/// Tells people in Telegram that a lesson was cancelled. Without the bot <see cref="NullCancellationNotifier"/> does nothing.
/// </summary>
public interface ICancellationNotifier
{
    /// <summary>
    /// An admin cancelled: the student and the parents (subscribed in the bot) are told.
    /// </summary>
    Task NotifyFamilyAsync(CancelledLessonInfo lesson, CancellationToken cancellationToken);

    /// <summary>
    /// The student or a parent cancelled: the bot admins are told.
    /// </summary>
    Task NotifyAdminsAsync(CancelledLessonInfo lesson, CancellationToken cancellationToken);
}

/// <summary>
/// Used while the Telegram bot is turned off.
/// </summary>
public sealed class NullCancellationNotifier : ICancellationNotifier
{
    public Task NotifyFamilyAsync(CancelledLessonInfo lesson, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task NotifyAdminsAsync(CancelledLessonInfo lesson, CancellationToken cancellationToken) => Task.CompletedTask;
}
