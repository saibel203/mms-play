using System.Text.Json;

namespace MMS.Play.Contracts.Models.Logging;

/// <summary>
/// A single log entry with the structured data of the event, shown when a row is expanded.
/// </summary>
/// <param name="Id">Log entry identifier.</param>
/// <param name="TimeStamp">When the event happened (UTC).</param>
/// <param name="Level">Verbose, Debug, Information, Warning, Error or Fatal.</param>
/// <param name="SourceContext">Class that wrote the entry.</param>
/// <param name="Message">Rendered message.</param>
/// <param name="MessageTemplate">Message before rendering, e.g. "Score {ScoreId} deleted": the same for every event of a kind.</param>
/// <param name="Exception">Exception with its stack trace, null when the entry has none.</param>
/// <param name="Properties">Values of the event (template arguments, RequestPath, MachineName...), null when they were not stored.</param>
public record LogEntryDetailsDto(
    int Id,
    DateTime TimeStamp,
    string? Level,
    string? SourceContext,
    string? Message,
    string? MessageTemplate,
    string? Exception,
    JsonElement? Properties);
