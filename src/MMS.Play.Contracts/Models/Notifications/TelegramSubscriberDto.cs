namespace MMS.Play.Contracts.Models.Notifications;

/// <summary>
/// A Telegram user who pressed Start in the bot.
/// </summary>
/// <param name="Id">Subscriber identifier.</param>
/// <param name="TelegramUserId">Permanent Telegram user id.</param>
/// <param name="Username">Telegram username without "@", optional in Telegram and may change.</param>
/// <param name="FirstName">First name from the Telegram profile.</param>
/// <param name="LastName">Last name from the Telegram profile.</param>
/// <param name="IsSubscribed">False after /stop or when the user blocked the bot. Set by the user only.</param>
/// <param name="RemindersEnabled">Set by an admin. Reminders are sent only when this and <paramref name="IsSubscribed"/> are true.</param>
/// <param name="SubscribedAt">Last time the user pressed Start (UTC).</param>
/// <param name="UnsubscribedAt">When the user unsubscribed or blocked the bot (UTC), null while subscribed.</param>
/// <param name="LastNotifiedAt">Last reminder delivered to Telegram (UTC).</param>
/// <param name="IsAdmin">Gets the students' reschedule requests in the bot and may approve or decline them there.</param>
/// <param name="IsParent">Gets the broadcasts for parents.</param>
/// <param name="Children">The students linked to this parent, by name.</param>
public record TelegramSubscriberDto(
    Guid Id,
    long TelegramUserId,
    string? Username,
    string FirstName,
    string? LastName,
    bool IsSubscribed,
    bool RemindersEnabled,
    DateTime SubscribedAt,
    DateTime? UnsubscribedAt,
    DateTime? LastNotifiedAt,
    bool IsAdmin,
    bool IsParent,
    IReadOnlyList<LinkedSubscriberDto> Children);
