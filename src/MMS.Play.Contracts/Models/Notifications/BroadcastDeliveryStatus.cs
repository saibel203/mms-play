namespace MMS.Play.Contracts.Models.Notifications;

public enum BroadcastDeliveryStatus
{
    /// <summary>
    /// Stored, being sent right now.
    /// </summary>
    Pending,

    Sent,

    /// <summary>
    /// Telegram did not take the message, see the error.
    /// </summary>
    Failed
}
