using System.ComponentModel.DataAnnotations;

namespace MMS.Play.Infrastructure.Models.Settings;

/// <summary>
/// Bound from the "Metrics" section in appsettings.
/// </summary>
public class MetricsSettings
{
    public const string SectionName = "Metrics";

    /// <summary>
    /// Pushes OpenTelemetry metrics to <see cref="OtlpEndpoint"/>. When disabled, nothing is collected.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Becomes the "job" label in Prometheus.
    /// </summary>
    [Required]
    public string ServiceName { get; set; } = "mms-play-api";

    /// <summary>
    /// Full OTLP/HTTP metrics URL. Prometheus accepts it at /api/v1/otlp/v1/metrics with --web.enable-otlp-receiver.
    /// </summary>
    [Required]
    [Url]
    public string OtlpEndpoint { get; set; } = "http://localhost:9090/api/v1/otlp/v1/metrics";

    [Range(5, 300)]
    public int ExportIntervalSeconds { get; set; } = 15;
}
