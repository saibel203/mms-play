using MMS.Play.Contracts.Models.Notifications;

namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// An invite link: "t.me/bot?start=inv_{Code}". Pressing Start with it registers the person with <see cref="Role"/>.
/// </summary>
public class Invite
{
    public Guid Id { get; set; }

    /// <summary>
    /// Random, URL safe; the part after "inv_" in the link.
    /// </summary>
    public required string Code { get; set; }

    public InviteRole Role { get; set; }

    public string? Label { get; set; }

    /// <summary>
    /// For a parent: the students they are linked to. Ids only, a deleted child is skipped.
    /// </summary>
    public List<Guid> ChildIds { get; set; } = [];

    /// <summary>
    /// Null for any number of people.
    /// </summary>
    public int? MaxUses { get; set; }

    /// <summary>
    /// The subscribers who used it, each once.
    /// </summary>
    public List<Guid> UsedByIds { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}
