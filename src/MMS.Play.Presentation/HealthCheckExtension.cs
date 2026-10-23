using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MMS.Play.Presentation;

public static class HealthCheckExtension
{
    public const string BasePath = "/health";

    /// <summary>
    /// /health/live - the process responds, runs no checks. Use for liveness probes and Docker HEALTHCHECK.
    /// /health/ready - runs every registered check and returns a JSON report. Use for readiness probes and monitoring.
    /// Unhealthy returns 503, Degraded still returns 200 so the instance keeps receiving traffic.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // a dependency outage must not restart the container, so liveness ignores the checks
        endpoints.MapHealthChecks($"{BasePath}/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        endpoints.MapHealthChecks($"{BasePath}/ready", new HealthCheckOptions
        {
            ResponseWriter = WriteJsonReportAsync
        });

        return endpoints;
    }

    // exceptions are left out on purpose: SqlException messages contain server names, the health check service logs them instead
    private static Task WriteJsonReportAsync(HttpContext context, HealthReport report)
    {
        var response = new
        {
            Status = report.Status.ToString(),
            report.TotalDuration,
            Checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    Status = entry.Value.Status.ToString(),
                    entry.Value.Description,
                    entry.Value.Duration,
                    entry.Value.Data
                })
        };

        return context.Response.WriteAsJsonAsync(response);
    }
}
