using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Logging;
using MMS.Play.Contracts.Models.Logging;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence;

namespace MMS.Play.Infrastructure.Logging;

public partial class LogCleanupService(
    ApplicationDbContext context,
    IOptions<LogCleanupSettings> options,
    ILogger<LogCleanupService> logger) : ILogCleanupService
{
    private readonly LogCleanupSettings _settings = options.Value;

    public async Task<LogCleanupResult> CleanupAsync(CancellationToken cancellationToken = default)
    {
        var cutoffUtc = DateTime.UtcNow.AddDays(-_settings.RetentionDays);

        var deletedRows = await DeleteDatabaseLogsAsync(cutoffUtc, cancellationToken);
        var deletedFiles = DeleteLogFiles(cutoffUtc, cancellationToken);

        return new LogCleanupResult(cutoffUtc, deletedRows, deletedFiles);
    }

    private async Task<int> DeleteDatabaseLogsAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        var total = 0;
        int deleted;

        // DELETE TOP(n) in a loop instead of one big DELETE: avoids lock escalation on the table
        // that the Serilog sink keeps writing to, and keeps every transaction short.
        do
        {
            deleted = await context.Logs
                .Where(x => x.TimeStamp < cutoffUtc)
                .Take(_settings.BatchSize)
                .ExecuteDeleteAsync(cancellationToken);

            total += deleted;
        }
        while (deleted == _settings.BatchSize);

        LogRowsDeleted(logger, total, cutoffUtc);
        return total;
    }

    private int DeleteLogFiles(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(_settings.LogFilesDirectory));
        if (!directory.Exists)
        {
            LogDirectoryNotFound(logger, directory.FullName);
            return 0;
        }

        var deleted = 0;

        foreach (var file in directory.EnumerateFiles(_settings.LogFilesPattern))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (file.LastWriteTimeUtc >= cutoffUtc) continue;

            try
            {
                file.Delete();
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogFileDeleteFailed(logger, ex, file.FullName);
            }
        }

        LogFilesDeleted(logger, deleted, cutoffUtc, directory.FullName);
        return deleted;
    }

    // Source-generated logging: arguments are evaluated only when the level is enabled (CA1873)
    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} log rows older than {CutoffUtc:u}")]
    private static partial void LogRowsDeleted(ILogger logger, int count, DateTime cutoffUtc);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Log files directory {Directory} does not exist, skipping file cleanup")]
    private static partial void LogDirectoryNotFound(ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not delete log file {File}")]
    private static partial void LogFileDeleteFailed(ILogger logger, Exception exception, string file);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} log files older than {CutoffUtc:u} from {Directory}")]
    private static partial void LogFilesDeleted(ILogger logger, int count, DateTime cutoffUtc, string directory);
}
