namespace MMS.Play.Contracts.Models.Logging;

/// <summary>
/// Log entry in a list: everything needed for a table row, without the heavy event JSON.
/// </summary>
/// <param name="Id">Log entry identifier.</param>
/// <param name="TimeStamp">When the event happened (UTC).</param>
/// <param name="Level">Verbose, Debug, Information, Warning, Error or Fatal.</param>
/// <param name="SourceContext">Class that wrote the entry, e.g. MMS.Play.Infrastructure.Scores.ScoreService.</param>
/// <param name="Message">Rendered message.</param>
/// <param name="Exception">Exception with its stack trace, null when the entry has none.</param>
public record LogEntryDto(
    int Id,
    DateTime TimeStamp,
    string? Level,
    string? SourceContext,
    string? Message,
    string? Exception);
