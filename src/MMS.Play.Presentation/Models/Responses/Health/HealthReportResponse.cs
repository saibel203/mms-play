using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MMS.Play.Presentation.Models.Responses.Health;

/// <summary>
/// Result of running every registered health check.
/// </summary>
/// <param name="Status">The worst status of all checks.</param>
/// <param name="TotalDurationMs">Time all checks took together, in milliseconds (they run in parallel).</param>
/// <param name="CheckedAt">When the checks ran (UTC).</param>
/// <param name="Checks">One entry per check, in registration order.</param>
public record HealthReportResponse(
    HealthStatus Status,
    double TotalDurationMs,
    DateTime CheckedAt,
    IReadOnlyList<HealthCheckEntryResponse> Checks);
