using System.ComponentModel.DataAnnotations;

namespace MMS.Play.Presentation.Models.Requests.Broadcasts;

/// <summary>
/// Query string of the broadcast history.
/// </summary>
public class ListBroadcastsRequest
{
    /// <summary>
    /// 1-based page number.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>
    /// Items per page, at most 100.
    /// </summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
