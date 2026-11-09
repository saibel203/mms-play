namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// The daily «time to practise» message went to the subscriber on <see cref="Date"/> (school date): one per day.
/// </summary>
public class HomeworkReminderDelivery
{
    public Guid SubscriberId { get; set; }

    public DateOnly Date { get; set; }

    public DateTime SentAtUtc { get; set; }
}
