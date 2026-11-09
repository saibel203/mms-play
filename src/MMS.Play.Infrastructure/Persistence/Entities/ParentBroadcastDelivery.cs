using MMS.Play.Contracts.Models.Notifications;

namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// One message of a <see cref="ParentBroadcast"/>: to one parent, about one lesson of their child with
/// <see cref="BroadcastAudience.LessonDay"/>. Keeps the parent's answer to a question.
/// The child and the lesson are copied, the delivery stays readable when the lesson is moved or deleted.
/// </summary>
public class ParentBroadcastDelivery
{
    public Guid Id { get; set; }

    public Guid BroadcastId { get; set; }
    public ParentBroadcast Broadcast { get; set; } = null!;

    public Guid ParentId { get; set; }
    public TelegramSubscriber Parent { get; set; } = null!;

    public long ChatId { get; set; }

    /// <summary>
    /// The sent message, to find it when the parent answers by replying to it. Null until sent.
    /// </summary>
    public int? MessageId { get; set; }

    /// <summary>
    /// The text as the parent got it (placeholders filled in), shown again with the answer added.
    /// </summary>
    public required string Text { get; set; }

    public string? ChildName { get; set; }
    public Guid? LessonId { get; set; }
    public DateOnly? LessonDate { get; set; }
    public TimeOnly? LessonTime { get; set; }

    public BroadcastDeliveryStatus Status { get; set; }

    /// <summary>
    /// Why Telegram did not take the message.
    /// </summary>
    public string? Error { get; set; }

    public DateTime? SentAtUtc { get; set; }

    /// <summary>
    /// The text of the pressed button or what the parent typed. The last answer wins.
    /// </summary>
    public string? Answer { get; set; }

    /// <summary>
    /// Index in <see cref="ParentBroadcast.Options"/> of the pressed button, null for a typed answer.
    /// </summary>
    public int? AnswerOption { get; set; }

    public DateTime? AnsweredAtUtc { get; set; }
}
