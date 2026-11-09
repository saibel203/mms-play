namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// A payment reminder about one student, sent to the student or one of their parents.
/// The latest one decides when that person is reminded again.
/// </summary>
public class PaymentReminderDelivery
{
    public Guid Id { get; set; }

    /// <summary>
    /// Who got it.
    /// </summary>
    public Guid SubscriberId { get; set; }

    /// <summary>
    /// Whose lessons it was about.
    /// </summary>
    public Guid StudentId { get; set; }

    public DateTime SentAtUtc { get; set; }

    /// <summary>
    /// Sent with the "Remind" button on the website, not by the schedule.
    /// </summary>
    public bool IsManual { get; set; }
}
