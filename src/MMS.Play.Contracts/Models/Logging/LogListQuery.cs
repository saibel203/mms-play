namespace MMS.Play.Contracts.Models.Logging;

/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page.</param>
/// <param name="MinLevel">Only entries of this level and more severe ones, all levels when null.</param>
/// <param name="FromUtc">Entries at or after this time.</param>
/// <param name="ToUtc">Entries before this time.</param>
/// <param name="Source">Start of the source context, e.g. "MMS.Play.Infrastructure" or a full class name.</param>
/// <param name="Search">Part of the message or the exception.</param>
public record LogListQuery(
    int Page,
    int PageSize,
    LogSeverity? MinLevel = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Source = null,
    string? Search = null);
