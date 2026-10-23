using System.ComponentModel.DataAnnotations;

namespace MMS.Play.Jobs.Models.Settings;

/// <summary>
/// Bound from the "Jobs:CleanupLogs" section in appsettings. Retention and batch size live in "LogCleanup".
/// </summary>
public class CleanupLogsJobSettings
{
    public const string SectionName = "Jobs:CleanupLogs";

    /// <summary>
    /// Cron expression, evaluated in UTC.
    /// </summary>
    [Required]
    public string Schedule { get; set; } = "0 3 * * *";
}
