using Microsoft.EntityFrameworkCore;
using MMS.Play.Contracts.Models.Homework;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Homeworks;

/// <summary>
/// A homework with what is shown about it, read in one query: the admin list, the practice page and the bot share it.
/// </summary>
public sealed class HomeworkRow
{
    public Guid Id { get; init; }
    public Guid StudentId { get; init; }
    public required string StudentFirstName { get; init; }
    public string? StudentLastName { get; init; }
    public Guid? ScoreId { get; init; }
    public string? ScoreTitle { get; init; }
    public string? ScoreArtist { get; init; }
    public string? ScoreExtension { get; init; }
    public required string Title { get; init; }
    public string? Instructions { get; init; }
    public int? FromBar { get; init; }
    public int? ToBar { get; init; }
    public int? StartTempoPercent { get; init; }
    public int? TargetTempoPercent { get; init; }
    public DateOnly? DueDate { get; init; }
    public HomeworkStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }

    /// <summary>
    /// The days it was marked practised, school dates.
    /// </summary>
    public List<DateOnly> PracticeDates { get; init; } = [];

    public string StudentName => StudentLastName is null ? StudentFirstName : $"{StudentFirstName} {StudentLastName}";

    public int Streak(DateOnly today) => HomeworkStats.Streak(PracticeDates, today);

    public bool PracticedOn(DateOnly day) => PracticeDates.Contains(day);

    public HomeworkDto ToDto(DateOnly today) => new(
        Id,
        StudentId,
        StudentName,
        ScoreId,
        ScoreTitle,
        ScoreArtist,
        Title,
        Instructions,
        FromBar,
        ToBar,
        StartTempoPercent,
        TargetTempoPercent,
        DueDate,
        Status,
        CreatedAtUtc,
        CompletedAtUtc,
        PracticeDates.Count,
        Streak(today),
        PracticedOn(today),
        PracticeDates.Count == 0 ? null : PracticeDates.Max());

    public PracticeHomeworkDto ToPracticeDto(DateOnly today) => new(
        Id,
        StudentName,
        Title,
        Instructions,
        ScoreId,
        ScoreTitle,
        ScoreArtist,
        ScoreExtension,
        FromBar,
        ToBar,
        StartTempoPercent,
        TargetTempoPercent,
        DueDate,
        PracticedOn(today),
        PracticeDates.Count,
        Streak(today));

    /// <summary>
    /// The homework of the query with its student, score and practice days. Ordered by the caller.
    /// </summary>
    public static Task<List<HomeworkRow>> ListAsync(IQueryable<Homework> homework, CancellationToken cancellationToken) =>
        homework
            .AsNoTracking()
            .Select(x => new HomeworkRow
            {
                Id = x.Id,
                StudentId = x.StudentId,
                StudentFirstName = x.Student.FirstName,
                StudentLastName = x.Student.LastName,
                ScoreId = x.ScoreId,
                ScoreTitle = x.Score == null ? null : x.Score.Title,
                ScoreArtist = x.Score == null ? null : x.Score.Artist,
                ScoreExtension = x.Score == null ? null : x.Score.Extension,
                Title = x.Title,
                Instructions = x.Instructions,
                FromBar = x.FromBar,
                ToBar = x.ToBar,
                StartTempoPercent = x.StartTempoPercent,
                TargetTempoPercent = x.TargetTempoPercent,
                DueDate = x.DueDate,
                Status = x.Status,
                CreatedAtUtc = x.CreatedAtUtc,
                CompletedAtUtc = x.CompletedAtUtc,
                PracticeDates = x.Practices.Select(practice => practice.Date).ToList()
            })
            .ToListAsync(cancellationToken);

    public static async Task<HomeworkRow?> FindAsync(ApplicationDbContext context, Guid id, CancellationToken cancellationToken) =>
        (await ListAsync(context.Homeworks.Where(x => x.Id == id), cancellationToken)).FirstOrDefault();
}
