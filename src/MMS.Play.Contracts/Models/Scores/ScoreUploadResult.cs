namespace MMS.Play.Contracts.Models.Scores;

/// <param name="Score">The stored score, the existing one when <paramref name="IsDuplicate"/> is true.</param>
/// <param name="IsDuplicate">A file with the same content had already been uploaded.</param>
public record ScoreUploadResult(ScoreDto Score, bool IsDuplicate);
