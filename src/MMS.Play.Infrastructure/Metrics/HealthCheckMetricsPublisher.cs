using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MMS.Play.Infrastructure.Metrics;

/// <summary>
/// Receives health reports from the background health check publisher (every 30 seconds by default)
/// and exposes the latest one as a gauge, so status history ends up in Prometheus.
/// </summary>
public class HealthCheckMetricsPublisher : IHealthCheckPublisher
{
    public const string MeterName = "MMS.Play.HealthChecks";
    private const string OverallCheckName = "overall";

    private volatile HealthReport? _lastReport;

    public HealthCheckMetricsPublisher(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        // values follow the HealthStatus enum, so higher is better
        meter.CreateObservableGauge(
            "mms_play.health_check.status",
            ObserveStatus,
            description: "Health check status: 0 - Unhealthy, 1 - Degraded, 2 - Healthy");
    }

    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        _lastReport = report;
        return Task.CompletedTask;
    }

    private IEnumerable<Measurement<int>> ObserveStatus()
    {
        var report = _lastReport;
        if (report is null)
            yield break;

        yield return new Measurement<int>((int)report.Status, new KeyValuePair<string, object?>("check", OverallCheckName));

        foreach (var (name, entry) in report.Entries)
            yield return new Measurement<int>((int)entry.Status, new KeyValuePair<string, object?>("check", name));
    }
}
