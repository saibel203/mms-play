using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.Models.Homework;

namespace MMS.Play.Presentation.Models.Requests.Homework;

/// <summary>
/// Homework given to a student or changed: a score from the library and/or what to do in words.
/// </summary>
public class HomeworkRequest
{
    /// <summary>
    /// The student, a Telegram subscriber who is not a parent.
    /// </summary>
    [Required]
    public Guid? StudentId { get; set; }

    /// <summary>
    /// A score from the library; empty for homework described in words only.
    /// </summary>
    public Guid? ScoreId { get; set; }

    /// <summary>
    /// Empty = the score's title.
    /// </summary>
    [StringLength(HomeworkDto.TitleMaxLength)]
    public string? Title { get; set; }

    /// <summary>
    /// What to do, plain text shown in the bot and on the practice page.
    /// </summary>
    [StringLength(HomeworkDto.InstructionsMaxLength)]
    public string? Instructions { get; set; }

    /// <summary>
    /// First bar to practise, 1-based; empty = from the beginning.
    /// </summary>
    [Range(1, HomeworkDto.MaxBar)]
    public int? FromBar { get; set; }

    /// <summary>
    /// Last bar to practise; empty = to the end.
    /// </summary>
    [Range(1, HomeworkDto.MaxBar)]
    public int? ToBar { get; set; }

    /// <summary>
    /// Tempo to start at, percent of the original (25 to 200).
    /// </summary>
    [Range(HomeworkDto.MinTempoPercent, HomeworkDto.MaxTempoPercent)]
    public int? StartTempoPercent { get; set; }

    /// <summary>
    /// Tempo to reach, percent of the original (25 to 200), not below the start.
    /// </summary>
    [Range(HomeworkDto.MinTempoPercent, HomeworkDto.MaxTempoPercent)]
    public int? TargetTempoPercent { get; set; }

    /// <summary>
    /// Until when, "2026-11-14"; empty = no deadline.
    /// </summary>
    public DateOnly? DueDate { get; set; }

    public HomeworkInput ToInput() =>
        new(StudentId!.Value, ScoreId, Title, Instructions, FromBar, ToBar, StartTempoPercent, TargetTempoPercent, DueDate);
}
