using System.ComponentModel.DataAnnotations;

namespace MMS.Play.Presentation.Models.Requests.Scores;

/// <summary>
/// Query string of the score list.
/// </summary>
public class ListScoresRequest
{
    public const int MaxPageSize = 100;

    /// <summary>
    /// 1-based page number.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>
    /// Items per page, at most 100.
    /// </summary>
    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Part of the title, artist or file name, case insensitive.
    /// </summary>
    [StringLength(100)]
    public string? Search { get; set; }
}
