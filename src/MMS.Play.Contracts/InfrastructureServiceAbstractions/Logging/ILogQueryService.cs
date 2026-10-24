using MMS.Play.Contracts.Models.Common;
using MMS.Play.Contracts.Models.Logging;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Logging;

/// <summary>
/// Reads the log entries Serilog writes to the database.
/// </summary>
public interface ILogQueryService
{
    /// <summary>
    /// Newest entries first.
    /// </summary>
    Task<PagedResult<LogEntryDto>> ListAsync(LogListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// The entry with its event properties, null when it does not exist (or was removed by the log cleanup).
    /// </summary>
    Task<LogEntryDetailsDto?> GetAsync(int id, CancellationToken cancellationToken = default);
}
