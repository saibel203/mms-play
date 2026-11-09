using MMS.Play.Contracts.Models.Homework;

namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// Homework given to a student: a score from the library (optional) with the bars and tempo to practise, and what to do.
/// </summary>
public class Homework
{
    /// <summary>
    /// Version 7 GUID: time ordered, so inserts do not fragment the clustered index.
    /// </summary>
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }
    public TelegramSubscriber Student { get; set; } = null!;

    /// <summary>
    /// Null for homework described by text only; set to null when the score is deleted from the library.
    /// </summary>
    public Guid? ScoreId { get; set; }
    public Score? Score { get; set; }

    public required string Title { get; set; }
    public string? Instructions { get; set; }

    /// <summary>
    /// 1-based bars as people count them; null = from the beginning / to the end.
    /// </summary>
    public int? FromBar { get; set; }
    public int? ToBar { get; set; }

    /// <summary>
    /// Percent of the original tempo (100 = original): start slower, reach the target.
    /// </summary>
    public int? StartTempoPercent { get; set; }
    public int? TargetTempoPercent { get; set; }

    /// <summary>
    /// School date.
    /// </summary>
    public DateOnly? DueDate { get; set; }

    public HomeworkStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public List<HomeworkPractice> Practices { get; set; } = [];
}
