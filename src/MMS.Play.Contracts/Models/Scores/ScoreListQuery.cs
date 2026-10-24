namespace MMS.Play.Contracts.Models.Scores;

/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page.</param>
/// <param name="Search">Matched against title, artist and file name, ignored when empty.</param>
public record ScoreListQuery(int Page, int PageSize, string? Search = null);
