using MMS.Play.Contracts.Models.Lessons;

namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// One lesson. <see cref="Date"/> and <see cref="StartTime"/> are the school's local time ("Lessons:TimeZoneId"),
/// so the timetable never shifts with daylight saving time.
/// </summary>
public class Lesson
{
    /// <summary>
    /// Version 7 GUID: time ordered, so inserts do not fragment the clustered index.
    /// </summary>
    public Guid Id { get; set; }

    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }

    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public Guid TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Null for a lesson described by <see cref="Note"/> only, e.g. a trial lesson with someone not in the bot yet.
    /// </summary>
    public Guid? StudentId { get; set; }
    public TelegramSubscriber? Student { get; set; }

    public string? Note { get; set; }

    /// <summary>
    /// Shared by the lessons created together as a weekly series ("repeat for N weeks") and their copies to later
    /// weeks, so "this and the following lessons" can be changed or deleted at once. Null for a one-off lesson.
    /// </summary>
    public Guid? SeriesId { get; set; }

    /// <summary>
    /// Rescheduled lessons stay where they were (crossed out on the website) and count nowhere.
    /// </summary>
    public LessonStatus Status { get; set; }

    /// <summary>
    /// The lesson that replaced this one when it was rescheduled.
    /// </summary>
    public Guid? RescheduledToId { get; set; }

    public List<LessonRescheduleRequest> RescheduleRequests { get; set; } = [];

    /// <summary>
    /// Copied from the default price when the lesson is created, so changing the default does not rewrite history.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Set with <see cref="LessonStatus.Cancelled"/>, cleared when the lesson is restored.
    /// </summary>
    public DateTime? CancelledAtUtc { get; set; }

    public CancellationSource? CancelledBy { get; set; }
    public string? CancelledByName { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary>
    /// Cancelled less than <see cref="LessonCancellationDto.LateHours"/> hours before the start, decided when cancelled.
    /// </summary>
    public bool IsLateCancellation { get; set; }

    public bool IsPaid { get; set; }
    public DateTime? PaidAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
