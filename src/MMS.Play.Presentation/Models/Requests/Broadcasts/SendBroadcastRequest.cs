using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.Models.Notifications;

namespace MMS.Play.Presentation.Models.Requests.Broadcasts;

/// <summary>
/// A message or a question for parents. With no options and allowTextAnswer false it is a plain message.
/// </summary>
public class SendBroadcastRequest
{
    /// <summary>
    /// AllParents: every parent, one message each. LessonDay: the parents of the students with a lesson on lessonDate, one message per lesson.
    /// </summary>
    [Required]
    public BroadcastAudience? Audience { get; set; }

    /// <summary>
    /// Required with LessonDay, "2026-11-12".
    /// </summary>
    public DateOnly? LessonDate { get; set; }

    /// <summary>
    /// Telegram HTML. Placeholders: {FirstName} (the parent), {ChildName}; with LessonDay also {LessonDay}, {LessonDate}, {LessonTime}, {Room}, {Teacher}.
    /// </summary>
    [Required]
    [StringLength(ParentBroadcastLimits.TextMaxLength)]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Answer buttons, up to 8, each up to 40 characters.
    /// </summary>
    [MaxLength(ParentBroadcastLimits.MaxOptions)]
    public List<string> Options { get; set; } = [];

    /// <summary>
    /// The parent may type an answer.
    /// </summary>
    public bool AllowTextAnswer { get; set; }

    /// <summary>
    /// Out of GET broadcasts/recipients with the same audience and day: parentId, and lessonId with LessonDay.
    /// </summary>
    [Required]
    [MaxLength(ParentBroadcastLimits.MaxRecipients)]
    public List<BroadcastTarget> Recipients { get; set; } = [];

    public BroadcastInput ToInput() => new(Audience!.Value, LessonDate, Text, Options, AllowTextAnswer, Recipients);
}
