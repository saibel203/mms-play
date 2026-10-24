using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MMS.Play.Presentation.Models.Responses.Health;

/// <summary>
/// Result of a single health check.
/// </summary>
/// <param name="Name">Check name: database, hangfire, document-storage.</param>
/// <param name="Status">Healthy, Degraded (works with limitations) or Unhealthy.</param>
/// <param name="Description">Explanation given by the check; for a failed check, the error message.</param>
/// <param name="DurationMs">Time the check took, in milliseconds.</param>
/// <param name="Data">Extra values reported by the check, e.g. Hangfire server and job counts.</param>
public record HealthCheckEntryResponse(
    string Name,
    HealthStatus Status,
    string? Description,
    double DurationMs,
    IReadOnlyDictionary<string, object> Data);
