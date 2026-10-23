using MMS.Play.Contracts.Models.Logging;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Logging;

public interface ILogCleanupService
{
    /// <summary>
    /// Deletes log rows from the database and log files older than the configured retention period.
    /// </summary>
    Task<LogCleanupResult> CleanupAsync(CancellationToken cancellationToken = default);
}
