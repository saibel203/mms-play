namespace MMS.Play.Contracts.Models.Notifications;

/// <summary>
/// Who a person becomes when they open an invite link in Telegram.
/// </summary>
public enum InviteRole
{
    Student,

    /// <summary>
    /// A parent, linked to the invite's children.
    /// </summary>
    Parent
}

/// <summary>
/// A link that registers a person in the bot with a role: they open it and press Start.
/// </summary>
/// <param name="Id">Invite identifier.</param>
/// <param name="Link">"https://t.me/bot?start=inv_…"; null while "Telegram:BotUsername" is not set.</param>
/// <param name="Role">Student or parent.</param>
/// <param name="Label">The admin's note, e.g. who it is for.</param>
/// <param name="Children">For a parent: the children they are linked to.</param>
/// <param name="MaxUses">How many people may use it, null for any number.</param>
/// <param name="UsedBy">Who used it, by name.</param>
/// <param name="CreatedAt">UTC.</param>
/// <param name="ExpiresAt">UTC, null for never.</param>
/// <param name="IsActive">Not expired and not used up: opening it still registers.</param>
public record InviteDto(
    Guid Id,
    string? Link,
    InviteRole Role,
    string? Label,
    IReadOnlyList<LinkedSubscriberDto> Children,
    int? MaxUses,
    IReadOnlyList<string> UsedBy,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    bool IsActive)
{
    public const int LabelMaxLength = 100;
    public const int MaxMaxUses = 1000;
    public const int MaxValidDays = 365;
}

/// <summary>
/// A new invite.
/// </summary>
/// <param name="Role">Student or parent.</param>
/// <param name="Label">Who it is for, optional.</param>
/// <param name="ChildIds">For a parent: the students they are linked to.</param>
/// <param name="MaxUses">1 for a personal link, null for one to share with many.</param>
/// <param name="ValidDays">Expires after this many days, null for never.</param>
public record InviteInput(InviteRole Role, string? Label, IReadOnlyList<Guid> ChildIds, int? MaxUses, int? ValidDays);
