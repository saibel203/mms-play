namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// The daily summary for admins went out on <see cref="Date"/> (school date), so the job sends it once a day.
/// </summary>
public class AdminDigestDelivery
{
    public DateOnly Date { get; set; }

    public DateTime SentAtUtc { get; set; }
}
