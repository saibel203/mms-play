using MMS.Play.Contracts.Models.Trial;

namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// A newcomer asking for a trial lesson, from the bot or the public page. An admin books it (creates the lesson) or
/// declines it on the website.
/// </summary>
public class TrialRequest
{
    /// <summary>
    /// Version 7 GUID: time ordered, so inserts do not fragment the clustered index.
    /// </summary>
    public Guid Id { get; set; }

    public TrialRequestSource Source { get; set; }

    /// <summary>
    /// Who asked in the bot; null for the public page.
    /// </summary>
    public Guid? SubscriberId { get; set; }
    public TelegramSubscriber? Subscriber { get; set; }

    /// <summary>
    /// Who comes to the lesson.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Phone or @telegram.
    /// </summary>
    public string? Contact { get; set; }

    public string? Comment { get; set; }

    /// <summary>
    /// The time picked, school time; null for "any time".
    /// </summary>
    public DateOnly? PreferredDate { get; set; }
    public TimeOnly? PreferredTime { get; set; }

    public TrialRequestStatus Status { get; set; }

    /// <summary>
    /// The lesson created when booked; cleared if that lesson is deleted.
    /// </summary>
    public Guid? LessonId { get; set; }
    public Lesson? Lesson { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
}
