using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Notifications;

/// <summary>
/// Sends one message of a broadcast for parents. Without the bot (Telegram:Enabled = false)
/// <see cref="NullParentBroadcastSender"/> is used and broadcasts are refused before anything is stored.
/// </summary>
public interface IParentBroadcastSender
{
    bool IsAvailable { get; }

    Task<BroadcastSendResult> SendAsync(ParentBroadcast broadcast, ParentBroadcastDelivery delivery, CancellationToken cancellationToken);
}

/// <param name="MessageId">The sent message, null when it was not sent.</param>
/// <param name="Error">Why it was not sent.</param>
/// <param name="Unreachable">The parent blocked the bot or deleted the chat: they are unsubscribed.</param>
public sealed record BroadcastSendResult(int? MessageId, string? Error, bool Unreachable = false);

/// <summary>
/// Used while the Telegram bot is turned off.
/// </summary>
public sealed class NullParentBroadcastSender : IParentBroadcastSender
{
    public bool IsAvailable => false;

    public Task<BroadcastSendResult> SendAsync(ParentBroadcast broadcast, ParentBroadcastDelivery delivery, CancellationToken cancellationToken) =>
        Task.FromResult(new BroadcastSendResult(null, "The Telegram bot is turned off."));
}
