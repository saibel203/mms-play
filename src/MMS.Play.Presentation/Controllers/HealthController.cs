using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MMS.Play.Contracts.Authorization;
using MMS.Play.Presentation.Controllers.Base;
using MMS.Play.Presentation.Models.Responses.Health;

namespace MMS.Play.Presentation.Controllers;

/// <summary>
/// Detailed health report for the admin page. Probes use the anonymous /health/live and /health/ready instead.
/// </summary>
[ApiVersion(1)]
[Authorize(Roles = Roles.Admin)]
public class HealthController(HealthCheckService healthCheckService) : BaseController
{
    /// <summary>
    /// Runs every health check and returns the result of each one.
    /// </summary>
    /// <remarks>
    /// Always answers 200, also when checks fail: the report itself is the result. Exceptions are left out,
    /// SqlException messages for example contain server names; the health check service logs them instead.
    /// </remarks>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The report.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpGet]
    [ProducesResponseType<HealthReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<HealthReportResponse> Get(CancellationToken cancellationToken)
    {
        var report = await healthCheckService.CheckHealthAsync(cancellationToken);

        return new HealthReportResponse(
            report.Status,
            report.TotalDuration.TotalMilliseconds,
            DateTime.UtcNow,
            [.. report.Entries.Select(entry => new HealthCheckEntryResponse(
                entry.Key,
                entry.Value.Status,
                entry.Value.Description,
                entry.Value.Duration.TotalMilliseconds,
                entry.Value.Data))]);
    }
}
