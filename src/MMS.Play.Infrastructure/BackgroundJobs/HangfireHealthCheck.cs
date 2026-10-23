using Hangfire;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MMS.Play.Infrastructure.BackgroundJobs;

/// <summary>
/// Checks that at least one Hangfire server is processing jobs. Failed jobs are reported as data only,
/// they are a business concern and must not take the instance out of rotation.
/// </summary>
public class HangfireHealthCheck(JobStorage storage) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // the monitoring API is synchronous; storage errors are caught by the health check service and reported with FailureStatus
        var monitoring = storage.GetMonitoringApi();
        var statistics = monitoring.GetStatistics();

        var data = new Dictionary<string, object>
        {
            ["servers"] = statistics.Servers,
            ["enqueued"] = statistics.Enqueued,
            ["processing"] = statistics.Processing,
            ["failed"] = statistics.Failed
        };

        var result = statistics.Servers > 0
            ? HealthCheckResult.Healthy("Hangfire servers are running", data)
            : new HealthCheckResult(context.Registration.FailureStatus, "No active Hangfire servers", data: data);

        return Task.FromResult(result);
    }
}
