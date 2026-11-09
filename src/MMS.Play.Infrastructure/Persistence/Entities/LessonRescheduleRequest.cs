namespace MMS.Play.Infrastructure.Persistence.Entities;

public enum RescheduleRequestStatus
{
    /// <summary>
    /// Waits for an admin.
    /// </summary>
    Pending,

    /// <summary>
    /// The lesson was moved to the proposed time.
    /// </summary>
    Approved,

    /// <summary>
    /// An admin said no, or the time got taken before the approval. The student picks another time;
    /// a declined time is shown crossed out in the bot.
    /// </summary>
    Declined,

    /// <summary>
    /// The student withdrew it.
    /// </summary>
    Cancelled
}

/// <summary>
/// A new time a student proposed for a lesson in the Telegram bot. Every proposal is a row,
/// so the declined ones of a lesson can be crossed out when the student picks again.
/// </summary>
public class LessonRescheduleRequest
{
    /// <summary>
    /// Version 7 GUID: time ordered, so inserts do not fragment the clustered index.
    /// </summary>
    public Guid Id { get; set; }

    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;

    public DateOnly ProposedDate { get; set; }
    public TimeOnly ProposedTime { get; set; }

    /// <summary>
    /// A room free at the proposed time when the student proposed it, the lesson's own room when possible.
    /// </summary>
    public Guid ProposedRoomId { get; set; }
    public Room ProposedRoom { get; set; } = null!;

    public RescheduleRequestStatus Status { get; set; }

    /// <summary>
    /// Where the student's answers go.
    /// </summary>
    public long StudentChatId { get; set; }

    /// <summary>
    /// The request messages sent to the admins, edited once it is answered.
    /// </summary>
    public List<TelegramMessageRef> AdminMessages { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }

    /// <summary>
    /// Admin's name, "website" or the student, for the other admins to see.
    /// </summary>
    public string? DecidedBy { get; set; }
}

/// <summary>
/// A message the bot sent, to edit it later.
/// </summary>
public class TelegramMessageRef
{
    public long ChatId { get; set; }
    public int MessageId { get; set; }
}
