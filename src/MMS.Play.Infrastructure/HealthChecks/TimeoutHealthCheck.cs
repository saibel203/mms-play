using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MMS.Play.Infrastructure.HealthChecks;

/// <summary>
/// Stops waiting for a check once its timeout token fires, even if the check ignores cancellation
/// (Hangfire monitoring API is synchronous, SqlClient may keep connecting for the whole Connection Timeout).
/// Without it a hanging check blocks /health/ready and the background publisher keeps the last healthy report.
/// </summary>
public class TimeoutHealthCheck(IHealthCheck inner) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // Task.Run so a check that blocks before returning its task cannot block the caller;
        // the health check service turns the cancellation into "A timeout occurred while running check." with FailureStatus
        return Task.Run(() => inner.CheckHealthAsync(context, cancellationToken), cancellationToken)
            .WaitAsync(cancellationToken);
    }
}
