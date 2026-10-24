using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;

namespace MMS.Play.Presentation;

public static class HealthCheckExtension
{
    public const string BasePath = "/health";

    /// <summary>
    /// /health/live - the process responds, runs no checks. Use for liveness probes and Docker HEALTHCHECK.
    /// /health/ready - runs every registered check and answers with the overall status only. Use for readiness probes.
    /// Unhealthy returns 503, Degraded still returns 200 so the instance keeps receiving traffic.
    /// Both are anonymous because probes cannot sign in, so neither shows details:
    /// the per-check report is GET /api/v1/health, admins only (HealthController).
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // a dependency outage must not restart the container, so liveness ignores the checks
        endpoints.MapHealthChecks($"{BasePath}/live", new HealthCheckOptions
        {
            Predicate = _ => false
        }).AllowAnonymous();

        endpoints.MapHealthChecks($"{BasePath}/ready").AllowAnonymous();

        return endpoints;
    }
}
