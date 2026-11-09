using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MMS.Play.Contracts.Authorization;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Trial;
using MMS.Play.Contracts.Models.Trial;
using MMS.Play.Presentation.Controllers.Base;
using MMS.Play.Presentation.Models.Requests.Trial;

namespace MMS.Play.Presentation.Controllers;

/// <summary>
/// Trial lessons for newcomers. The free times and sending a request are public (the page /trial, no sign in);
/// the list of requests and answering them are for admins. Dates and times are school time.
/// </summary>
[ApiVersion(1)]
public class TrialController(ITrialBookingService trialService) : BaseController
{
    /// <summary>
    /// Free times for a trial lesson over the next days.
    /// </summary>
    /// <response code="200">The days with free times; IsOpen false and no days while booking is turned off.</response>
    [HttpGet("slots")]
    [AllowAnonymous]
    [ProducesResponseType<TrialSlotsDto>(StatusCodes.Status200OK)]
    public Task<TrialSlotsDto> GetSlots(CancellationToken cancellationToken) =>
        trialService.GetSlotsAsync(cancellationToken);

    /// <summary>
    /// Sends a trial lesson request; the admins get it in the bot.
    /// </summary>
    /// <response code="202">Received; the school contacts the person.</response>
    /// <response code="400">Booking is off, a field is invalid, the time got taken, a request with this contact waits already, or too many requests lately.</response>
    [HttpPost("requests")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> CreateRequest(CreateTrialRequestRequest request, CancellationToken cancellationToken)
    {
        // a bot filled in the hidden field: it is told all went well, so it does not try another way
        if (!string.IsNullOrEmpty(request.Website))
            return Accepted();

        await trialService.CreateFromWebsiteAsync(request.ToInput(), cancellationToken);
        return Accepted();
    }

    /// <summary>
    /// Trial lesson requests, newest first.
    /// </summary>
    /// <param name="status">Only this status; all when left out.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The requests.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpGet("requests")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<IReadOnlyList<TrialRequestDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public Task<IReadOnlyList<TrialRequestDto>> ListRequests([FromQuery] TrialRequestStatus? status, CancellationToken cancellationToken) =>
        trialService.ListAsync(status, cancellationToken);

    /// <summary>
    /// Books the trial lesson: creates the lesson (with the usual clash checks) and tells a bot requester.
    /// </summary>
    /// <response code="200">The request, booked, with the lesson.</response>
    /// <response code="400">Already answered, or the lesson is invalid or clashes.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">No such request.</response>
    [HttpPost("requests/{id:guid}/book")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<TrialRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<TrialRequestDto>> Book(Guid id, BookTrialRequestRequest request, CancellationToken cancellationToken) =>
        await trialService.BookAsync(id, request.ToInput(), cancellationToken) is { } booked ? booked : NotFound();

    /// <summary>
    /// Turns the request down; a bot requester is told, with the reason when given.
    /// </summary>
    /// <response code="200">The request, declined.</response>
    /// <response code="400">Already answered.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">No such request.</response>
    [HttpPost("requests/{id:guid}/decline")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<TrialRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<TrialRequestDto>> Decline(Guid id, DeclineTrialRequestRequest request, CancellationToken cancellationToken) =>
        await trialService.DeclineAsync(id, request.Reason, cancellationToken) is { } declined ? declined : NotFound();
}
