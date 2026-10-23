using Hangfire;
using Microsoft.Extensions.Logging;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Logging;

namespace MMS.Play.Jobs.Logging;

/// <summary>
/// Daily removal of log rows and log files older than "LogCleanup:RetentionDays".
/// </summary>
public partial class CleanupLogsJob(ILogCleanupService logCleanupService, ILogger<CleanupLogsJob> logger)
{
    public const string JobId = "cleanup-logs";

    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(timeoutInSeconds: 10 * 60)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await logCleanupService.CleanupAsync(cancellationToken);
        LogCleanupFinished(logger, result.DeletedRows, result.DeletedFiles, result.CutoffUtc);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Log cleanup finished: {DeletedRows} rows and {DeletedFiles} files older than {CutoffUtc:u} deleted")]
    private static partial void LogCleanupFinished(ILogger logger, int deletedRows, int deletedFiles, DateTime cutoffUtc);
}
