using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MMS.Play.Contracts.Authorization;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Logging;
using MMS.Play.Contracts.Models.Common;
using MMS.Play.Contracts.Models.Logging;
using MMS.Play.Presentation.Controllers.Base;
using MMS.Play.Presentation.Models.Requests.Logs;

namespace MMS.Play.Presentation.Controllers;

/// <summary>
/// Log entries written by Serilog to the database, admins only. Entries older than "LogCleanup:RetentionDays" are removed daily.
/// </summary>
[ApiVersion(1)]
[Authorize(Roles = Roles.Admin)]
public class LogsController(ILogQueryService logQueryService) : BaseController
{
    /// <summary>
    /// Lists log entries, newest first.
    /// </summary>
    /// <remarks>
    /// Rows carry no event properties, request a single entry for them.
    /// </remarks>
    /// <response code="200">The requested page, empty when it is past the last one.</response>
    /// <response code="400">Invalid paging or filter parameters.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<LogEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public Task<PagedResult<LogEntryDto>> List([FromQuery] ListLogsRequest request, CancellationToken cancellationToken) =>
        logQueryService.ListAsync(request.ToQuery(), cancellationToken);

    /// <summary>
    /// Returns a log entry with its message template and event properties.
    /// </summary>
    /// <param name="id">Log entry identifier.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The entry.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no entry with this identifier, it may have been removed by the log cleanup.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<LogEntryDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<LogEntryDetailsDto>> Get(int id, CancellationToken cancellationToken)
    {
        var entry = await logQueryService.GetAsync(id, cancellationToken);
        if (entry is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Log entry not found", detail: $"Log entry {id} does not exist.");

        return entry;
    }
}
