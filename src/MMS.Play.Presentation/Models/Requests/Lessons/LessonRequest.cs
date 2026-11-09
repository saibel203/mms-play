using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.Models.Lessons;

namespace MMS.Play.Presentation.Models.Requests.Lessons;

/// <summary>
/// Fields of a lesson, shared by creating and editing it. Dates and times are school time.
/// </summary>
public abstract class LessonRequest
{
    /// <summary>
    /// Day of the lesson, "2026-11-12".
    /// </summary>
    [Required]
    public DateOnly? Date { get; set; }

    /// <summary>
    /// Start, "14:00:00"; between 07:00 and 22:00. The lesson lasts 60 minutes.
    /// </summary>
    [Required]
    public TimeOnly? StartTime { get; set; }

    [Required]
    public Guid? RoomId { get; set; }

    [Required]
    public Guid? TeacherId { get; set; }

    /// <summary>
    /// Telegram subscriber; may be left out when <see cref="Note"/> describes the lesson (e.g. a trial lesson).
    /// </summary>
    public Guid? StudentId { get; set; }

    [StringLength(LessonDto.NoteMaxLength)]
    public string? Note { get; set; }

    public bool IsPaid { get; set; }

    protected LessonInput ToInput(decimal price) =>
        new(Date!.Value, StartTime!.Value, RoomId!.Value, TeacherId!.Value, StudentId, Note, price, IsPaid);
}
