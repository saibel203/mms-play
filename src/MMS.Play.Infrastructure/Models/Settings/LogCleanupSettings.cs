using System.ComponentModel.DataAnnotations;

namespace MMS.Play.Infrastructure.Models.Settings;

/// <summary>
/// Bound from the "LogCleanup" section in appsettings.
/// </summary>
public class LogCleanupSettings
{
    public const string SectionName = "LogCleanup";

    /// <summary>
    /// Path template of the Serilog file sink, the single source for where log files are written.
    /// </summary>
    public const string SerilogFilePathKey = "Serilog:WriteTo:File:Args:path";

    /// <summary>
    /// Log rows and files older than this number of days are deleted.
    /// </summary>
    [Range(1, 3650)]
    public int RetentionDays { get; set; } = 7;

    /// <summary>
    /// Rows deleted per statement, keeps locks and transaction log small.
    /// </summary>
    [Range(1, 100_000)]
    public int BatchSize { get; set; } = 5000;

    /// <summary>
    /// Filled from <see cref="SerilogFilePathKey"/>, not from the "LogCleanup" section. Null when the file sink is not configured.
    /// </summary>
    public string? LogFilePath { get; set; }
}
