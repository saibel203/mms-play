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
/// The teachers of the timetable. Admins only.
/// </summary>
[ApiVersion(1)]
[Authorize(Roles = Roles.Admin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public class TeachersController(ISchoolResourceService resourceService) : BaseController
{
    /// <summary>
    /// Lists the teachers in timetable order with their lesson counts.
    /// </summary>
    /// <response code="200">The teachers.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TeacherSummaryDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<TeacherSummaryDto>> List(CancellationToken cancellationToken) =>
        resourceService.ListTeachersAsync(cancellationToken);

    /// <summary>
    /// Adds a teacher at the end of the list.
    /// </summary>
    /// <response code="201">The new teacher.</response>
    /// <response code="400">An empty or taken name, or an invalid color.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpPost]
    [ProducesResponseType<TeacherSummaryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Create(TeacherRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await resourceService.CreateTeacherAsync(request.ToInput(), cancellationToken));

    /// <summary>
    /// Renames a teacher or changes the color; the lessons keep the teacher.
    /// </summary>
    /// <param name="id">Teacher identifier.</param>
    /// <param name="request">The new name and color.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The updated teacher.</response>
    /// <response code="400">An empty or taken name, or an invalid color.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no teacher with this identifier.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<TeacherSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<TeacherSummaryDto>> Update(Guid id, TeacherRequest request, CancellationToken cancellationToken)
    {
        var teacher = await resourceService.UpdateTeacherAsync(id, request.ToInput(), cancellationToken);
        return teacher is null ? NotFoundProblem(id) : teacher;
    }

    /// <summary>
    /// Deletes a teacher without lessons.
    /// </summary>
    /// <param name="id">Teacher identifier.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="400">The teacher has lessons, errors.Id says how many.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no teacher with this identifier.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await resourceService.DeleteTeacherAsync(id, cancellationToken) ? NoContent() : NotFoundProblem(id);

    private ObjectResult NotFoundProblem(Guid id) =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Teacher not found", detail: $"Teacher '{id}' does not exist.");
}
