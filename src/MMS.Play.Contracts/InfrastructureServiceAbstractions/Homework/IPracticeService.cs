using MMS.Play.Contracts.Models.Homework;
using MMS.Play.Contracts.Models.Scores;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Homework;

/// <summary>
/// The public practice page behind a personal link from the bot (no sign in): <c>token</c> is the subscriber's
/// access token. An unknown token, or homework that is not the owner's (or their child's), gives null.
/// </summary>
public interface IPracticeService
{
    Task<PracticeOverviewDto?> GetOverviewAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// The score file of an active homework; the caller disposes <see cref="ScoreContent.Content"/>.
    /// </summary>
    Task<ScoreContent?> OpenScoreAsync(string token, Guid homeworkId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an active homework practised today (school date); doing it again changes nothing.
    /// </summary>
    Task<PracticeHomeworkDto?> MarkPracticedAsync(string token, Guid homeworkId, CancellationToken cancellationToken = default);
}
