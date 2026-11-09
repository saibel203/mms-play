namespace MMS.Play.Contracts.Models.Homework;

/// <summary>
/// Homework given to a student: what to practise (a score from the library and its bars, a tempo) and how it goes.
/// </summary>
/// <param name="StudentId">The student, a Telegram subscriber.</param>
/// <param name="ScoreId">The score from the library, null for homework described by text only (or a deleted score).</param>
/// <param name="FromBar">First bar to practise (1-based), null = from the beginning.</param>
/// <param name="ToBar">Last bar to practise, null = to the end.</param>
/// <param name="StartTempoPercent">Tempo to start at, percent of the original (100 = original).</param>
/// <param name="TargetTempoPercent">Tempo to reach.</param>
/// <param name="DueDate">Until when, school date; null = no deadline.</param>
/// <param name="CreatedAt">When it was given (UTC).</param>
/// <param name="CompletedAt">When it was marked done (UTC).</param>
/// <param name="PracticeDays">On how many days the student marked it practised.</param>
/// <param name="Streak">Days in a row up to today (or yesterday, when today is not marked yet).</param>
/// <param name="PracticedToday">Marked practised today, school date.</param>
/// <param name="LastPracticedOn">The last day it was marked practised.</param>
public record HomeworkDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    Guid? ScoreId,
    string? ScoreTitle,
    string? ScoreArtist,
    string Title,
    string? Instructions,
    int? FromBar,
    int? ToBar,
    int? StartTempoPercent,
    int? TargetTempoPercent,
    DateOnly? DueDate,
    HomeworkStatus Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    int PracticeDays,
    int Streak,
    bool PracticedToday,
    DateOnly? LastPracticedOn)
{
    public const int TitleMaxLength = 200;
    public const int InstructionsMaxLength = 2000;
    public const int MaxBar = 999;
    public const int MinTempoPercent = 25;
    public const int MaxTempoPercent = 200;
}

/// <summary>
/// What an admin fills in when giving or changing homework.
/// </summary>
/// <param name="Title">Null or empty = the score's title.</param>
public record HomeworkInput(
    Guid StudentId,
    Guid? ScoreId,
    string? Title,
    string? Instructions,
    int? FromBar,
    int? ToBar,
    int? StartTempoPercent,
    int? TargetTempoPercent,
    DateOnly? DueDate);

/// <param name="StudentId">Only this student's homework.</param>
/// <param name="Status">Only active or only completed; null = both.</param>
public record HomeworkListQuery(Guid? StudentId, HomeworkStatus? Status);
