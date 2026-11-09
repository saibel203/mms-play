using System.ComponentModel.DataAnnotations;

namespace MMS.Play.Presentation.Models.Requests.Telegram;

/// <summary>
/// The "parent" checkbox of one subscriber and their children.
/// </summary>
public class SetSubscriberParentRequest
{
    /// <summary>
    /// true: gets the broadcasts for parents.
    /// </summary>
    // nullable so a missing field is a 400 instead of silently taking the role away
    [Required]
    public bool? IsParent { get; set; }

    /// <summary>
    /// The children: students (subscribers who are not parents). Replaces the linked ones, empty unlinks all.
    /// </summary>
    [MaxLength(10)]
    public List<Guid> ChildIds { get; set; } = [];
}
