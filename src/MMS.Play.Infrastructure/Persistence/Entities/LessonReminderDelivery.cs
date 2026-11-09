namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// A reminder sent for a lesson to one recipient (the student or a parent) at one reminder slot (a day and one of the
/// reminder times). Keeps the job from sending the same reminder twice when it runs again or overlaps.
/// </summary>
public class LessonReminderDelivery
{
    public Guid LessonId { get; set; }

    /// <summary>
    /// Who got it: the student of the lesson or one of the student's parents.
    /// </summary>
    public Guid SubscriberId { get; set; }

    /// <summary>
    /// The reminder slot the message was sent for (UTC), not when it was sent.
    /// </summary>
    public DateTime SlotUtc { get; set; }

    public DateTime SentAtUtc { get; set; }
}
