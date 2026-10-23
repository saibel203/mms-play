namespace MMS.Play.Infrastructure.Models.Settings;

/// <summary>
/// Bound from the "Hangfire" section in appsettings.
/// </summary>
public class HangfireSettings
{
    public const string SectionName = "Hangfire";

    public string SchemaName { get; set; } = "hangfire";
    public int WorkerCount { get; set; } = 5;
    public string[] Queues { get; set; } = ["default"];
    public HangfireDashboardSettings Dashboard { get; set; } = new();
}

public class HangfireDashboardSettings
{
    public bool Enabled { get; set; } = true;
    public string Path { get; set; } = "/hangfire";
    public string Title { get; set; } = "MMS.Play Jobs";

    /// <summary>
    /// Basic auth credentials. When empty, the dashboard rejects every request.
    /// </summary>
    public string? Username { get; set; }
    public string? Password { get; set; }

    /// <summary>
    /// Hides delete/requeue/trigger buttons.
    /// </summary>
    public bool ReadOnly { get; set; }
}
