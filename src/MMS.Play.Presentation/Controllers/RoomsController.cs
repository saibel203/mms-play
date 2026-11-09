using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MMS.Play.Contracts.Authorization;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Lessons;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Presentation.Controllers.Base;
using MMS.Play.Presentation.Models.Requests.Lessons;

namespace MMS.Play.Presentation.Controllers;

/// <summary>
/// The rooms of the timetable and their hours. Admins only.
/// </summary>
[ApiVersion(1)]
[Authorize(Roles = Roles.Admin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public class RoomsController(ISchoolResourceService resourceService) : BaseController
{
    /// <summary>
    /// Lists the rooms in timetable order with their hours and lesson counts.
    /// </summary>
    /// <response code="200">The rooms.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RoomSummaryDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<RoomSummaryDto>> List(CancellationToken cancellationToken) =>
        resourceService.ListRoomsAsync(cancellationToken);

    /// <summary>
    /// Adds a room at the end of the timetable.
    /// </summary>
    /// <response code="201">The new room.</response>
    /// <response code="400">An empty or taken name, or invalid hours.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpPost]
    [ProducesResponseType<RoomSummaryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Create(RoomRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await resourceService.CreateRoomAsync(request.ToInput(), cancellationToken));

    /// <summary>
    /// Renames a room or changes its hours; existing lessons stay where they are.
    /// </summary>
    /// <param name="id">Room identifier.</param>
    /// <param name="request">The new name and hours.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The updated room.</response>
    /// <response code="400">An empty or taken name, or invalid hours.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no room with this identifier.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<RoomSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<RoomSummaryDto>> Update(Guid id, RoomRequest request, CancellationToken cancellationToken)
    {
        var room = await resourceService.UpdateRoomAsync(id, request.ToInput(), cancellationToken);
        return room is null ? NotFoundProblem(id) : room;
    }

    /// <summary>
    /// Deletes a room without lessons.
    /// </summary>
    /// <param name="id">Room identifier.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="400">The room has lessons or a reschedule request proposes it.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no room with this identifier.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await resourceService.DeleteRoomAsync(id, cancellationToken) ? NoContent() : NotFoundProblem(id);

    private ObjectResult NotFoundProblem(Guid id) =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Room not found", detail: $"Room '{id}' does not exist.");
}
