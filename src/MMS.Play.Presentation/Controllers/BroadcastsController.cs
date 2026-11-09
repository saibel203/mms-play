using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MMS.Play.Contracts.Authorization;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;
using MMS.Play.Contracts.Models.Common;
using MMS.Play.Contracts.Models.Notifications;
using MMS.Play.Presentation.Controllers.Base;
using MMS.Play.Presentation.Models.Requests.Broadcasts;

namespace MMS.Play.Presentation.Controllers;

/// <summary>
/// Messages and questions for parents in the Telegram bot, and their answers. Admins only.
/// Parents are the subscribers marked as parents on the Telegram page, linked to their children.
/// </summary>
[ApiVersion(1)]
[Authorize(Roles = Roles.Admin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public class BroadcastsController(IParentBroadcastService broadcastService) : BaseController
{
    /// <summary>
    /// Lists who would get a broadcast, for the admin to check before sending.
    /// </summary>
    /// <param name="audience">AllParents, or LessonDay: the parents of the students with a lesson on lessonDate (one row per lesson).</param>
    /// <param name="lessonDate">Required with LessonDay, "2026-11-12".</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The recipients; isSubscribed: false ones would not get the message.</response>
    /// <response code="400">LessonDay without lessonDate.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpGet("recipients")]
    [ProducesResponseType<IReadOnlyList<BroadcastRecipientDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<IReadOnlyList<BroadcastRecipientDto>> ListRecipients(
        [FromQuery] BroadcastAudience audience,
        [FromQuery] DateOnly? lessonDate,
        CancellationToken cancellationToken) =>
        broadcastService.ListRecipientsAsync(audience, lessonDate, cancellationToken);

    /// <summary>
    /// Sends a message or a question to the picked parents right away.
    /// </summary>
    /// <remarks>
    /// The request lasts until every message is sent (about 25 per second). A question has answer buttons (options),
    /// accepts a typed answer (allowTextAnswer) or both; the answers show up in GET broadcasts/{id}.
    /// Unsubscribed parents are stored as failed without sending.
    /// </remarks>
    /// <response code="201">The broadcast with every message.</response>
    /// <response code="400">Invalid text or options, recipients no longer on the list, or the bot is turned off; errors say which.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpPost]
    [ProducesResponseType<BroadcastDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<BroadcastDetailsDto>> Send(SendBroadcastRequest request, CancellationToken cancellationToken)
    {
        var broadcast = await broadcastService.SendAsync(request.ToInput(), User.Identity?.Name, cancellationToken);
        // link generation drops ambient route values when the target action differs, so the version segment is passed as requested
        return CreatedAtAction(nameof(Get), new { id = broadcast.Id, version = RouteData.Values["version"] }, broadcast);
    }

    /// <summary>
    /// Lists the sent broadcasts, latest first, with how many were delivered and answered.
    /// </summary>
    /// <response code="200">The requested page, empty when it is past the last one.</response>
    /// <response code="400">Invalid paging parameters.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<BroadcastSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PagedResult<BroadcastSummaryDto>> List([FromQuery] ListBroadcastsRequest request, CancellationToken cancellationToken) =>
        broadcastService.ListAsync(request.Page, request.PageSize, cancellationToken);

    /// <summary>
    /// Returns a broadcast with every message and the parents' answers.
    /// </summary>
    /// <param name="id">Broadcast identifier.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The broadcast.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no broadcast with this identifier.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<BroadcastDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<BroadcastDetailsDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var broadcast = await broadcastService.GetAsync(id, cancellationToken);
        return broadcast is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Broadcast not found", detail: $"Broadcast '{id}' does not exist.")
            : broadcast;
    }
}
