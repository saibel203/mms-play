using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.Models.Notifications;

namespace MMS.Play.Presentation.Models.Requests.Telegram;

/// <summary>
/// A new invite link.
/// </summary>
public class CreateInviteRequest
{
    /// <summary>
    /// Student or Parent.
    /// </summary>
    [Required]
    public InviteRole? Role { get; set; }

    /// <summary>
    /// Who it is for, e.g. "Олена, мама Олега"; optional.
    /// </summary>
    [StringLength(InviteDto.LabelMaxLength)]
    public string? Label { get; set; }

    /// <summary>
    /// For a parent: the students to link. Up to 10.
    /// </summary>
    [MaxLength(10)]
    public List<Guid> ChildIds { get; set; } = [];

    /// <summary>
    /// 1 for a personal link, empty for a link many people may use.
    /// </summary>
    [Range(1, InviteDto.MaxMaxUses)]
    public int? MaxUses { get; set; }

    /// <summary>
    /// Days the link works, empty for no limit.
    /// </summary>
    [Range(1, InviteDto.MaxValidDays)]
    public int? ValidDays { get; set; }

    public InviteInput ToInput() => new(Role!.Value, Label, ChildIds, MaxUses, ValidDays);
}
