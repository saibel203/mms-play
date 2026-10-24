namespace MMS.Play.Contracts.Models.Common;

/// <summary>
/// One page of a list.
/// </summary>
/// <param name="Items">Items of the requested page, empty when the page is past the end.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Maximum number of items per page.</param>
/// <param name="TotalCount">Number of items on all pages.</param>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    /// <summary>
    /// Number of pages, 0 when there are no items.
    /// </summary>
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
}
