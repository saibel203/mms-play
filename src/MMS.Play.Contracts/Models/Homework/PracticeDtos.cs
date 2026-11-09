namespace MMS.Play.Contracts.Models.Homework;

/// <summary>
/// The public practice page (opened from the bot by a personal link): the active homework of the link's owner and,
/// for a parent, of their children.
/// </summary>
/// <param name="Name">The link owner's first name.</param>
/// <param name="Homework">Active homework, newest first.</param>
public record PracticeOverviewDto(string Name, IReadOnlyList<PracticeHomeworkDto> Homework);

/// <summary>
/// One homework as the student sees it, see <see cref="HomeworkDto"/> for the fields.
/// </summary>
/// <param name="ScoreExtension">Lower case file extension of the score ("gp", "musicxml"), how the player loads it.</param>
public record PracticeHomeworkDto(
    Guid Id,
    string StudentName,
    string Title,
    string? Instructions,
    Guid? ScoreId,
    string? ScoreTitle,
    string? ScoreArtist,
    string? ScoreExtension,
    int? FromBar,
    int? ToBar,
    int? StartTempoPercent,
    int? TargetTempoPercent,
    DateOnly? DueDate,
    bool PracticedToday,
    int PracticeDays,
    int Streak);
