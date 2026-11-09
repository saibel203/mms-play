using MMS.Play.Contracts.Models.Notifications;

namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// A message an admin sent to parents in the bot: a plain message or a question. A question has answer buttons
/// (<see cref="Options"/>), accepts a typed answer (<see cref="AllowTextAnswer"/>) or both.
/// </summary>
public class ParentBroadcast
{
    /// <summary>
    /// Version 7 GUID: time ordered, so inserts do not fragment the clustered index.
    /// </summary>
    public Guid Id { get; set; }

    public BroadcastAudience Audience { get; set; }

    /// <summary>
    /// The lesson day of <see cref="BroadcastAudience.LessonDay"/>, null for every parent.
    /// </summary>
    public DateOnly? LessonDate { get; set; }

    /// <summary>
    /// Telegram HTML with placeholders, as the admin wrote it. Every parent gets it filled in, see <see cref="ParentBroadcastDelivery.Text"/>.
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// Answer buttons in the order shown, empty for a plain message or a typed answer only.
    /// </summary>
    public List<string> Options { get; set; } = [];

    public bool AllowTextAnswer { get; set; }

    /// <summary>
    /// The admin's user name on the website.
    /// </summary>
    public string? CreatedBy { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public List<ParentBroadcastDelivery> Deliveries { get; set; } = [];

    public bool IsQuestion => Options.Count > 0 || AllowTextAnswer;
}
