namespace MMS.Play.Infrastructure.Models.Settings;

/// <summary>
/// Bound from the "LogCleanup" section in appsettings.
/// </summary>
public class LogCleanupSettings
{
    public const string SectionName = "LogCleanup";

    /// <summary>
    /// Cron expression (UTC) for the recurring cleanup job.
    /// </summary>
    public string Schedule { get; set; } = "0 3 * * *";

    /// <summary>
    /// Log rows and files older than this number of days are deleted.
    /// </summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>
    /// Rows deleted per statement, keeps locks and transaction log small.
    /// </summary>
    public int BatchSize { get; set; } = 5000;

    /// <summary>
    /// Must match the directory of "Serilog:WriteTo:File:Args:path". Relative paths are resolved against the current directory, same as the Serilog file sink.
    /// </summary>
    public string LogFilesDirectory { get; set; } = "logs";

    public string LogFilesPattern { get; set; } = "log-*.txt";
}
