namespace MMS.Play.Infrastructure.Homeworks;

/// <summary>
/// Tells a student in Telegram about new homework. Without the bot <see cref="NullHomeworkNotifier"/> does nothing.
/// A message that does not go out is logged, never thrown: the homework is stored and shows on the practice page anyway.
/// </summary>
public interface IHomeworkNotifier
{
    Task NotifyAssignedAsync(Guid homeworkId, CancellationToken cancellationToken);
}

/// <summary>
/// Used while the Telegram bot is turned off.
/// </summary>
public sealed class NullHomeworkNotifier : IHomeworkNotifier
{
    public Task NotifyAssignedAsync(Guid homeworkId, CancellationToken cancellationToken) => Task.CompletedTask;
}
