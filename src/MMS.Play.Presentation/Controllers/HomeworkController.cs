using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MMS.Play.Contracts.Authorization;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Homework;
using MMS.Play.Contracts.Models.Homework;
using MMS.Play.Presentation.Controllers.Base;
using MMS.Play.Presentation.Models.Requests.Homework;

namespace MMS.Play.Presentation.Controllers;

/// <summary>
/// Homework for students: a score from the library with bars and tempo, and what to do. The student is told in the
/// bot, reminded daily to practise and marks practice with «✅ Позаймався». Admins only.
/// </summary>
[ApiVersion(1)]
[Authorize(Roles = Roles.Admin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public class HomeworkController(IHomeworkService homeworkService) : BaseController
{
    /// <summary>
    /// Lists homework, active first, then newest first.
    /// </summary>
    /// <response code="200">The homework with practice stats.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<HomeworkDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<HomeworkDto>> List([FromQuery] ListHomeworkRequest request, CancellationToken cancellationToken) =>
        homeworkService.ListAsync(request.ToQuery(), cancellationToken);

    /// <summary>
    /// Gives a student homework; they get the «HomeworkAssigned» message in the bot.
    /// </summary>
    /// <response code="201">The homework.</response>
    /// <response code="400">Unknown, unsubscribed or parent student, unknown score, no title, bars or tempo out of range.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpPost]
    [ProducesResponseType<HomeworkDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Create(HomeworkRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await homeworkService.CreateAsync(request.ToInput(), cancellationToken));

    /// <summary>
    /// Changes homework; the student is not told again, the practice marks stay.
    /// </summary>
    /// <param name="id">Homework identifier.</param>
    /// <param name="request">The new values.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The homework.</response>
    /// <response code="400">See create.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no homework with this identifier.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<HomeworkDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<HomeworkDto>> Update(Guid id, HomeworkRequest request, CancellationToken cancellationToken) =>
        await homeworkService.UpdateAsync(id, request.ToInput(), cancellationToken) is { } homework ? homework : HomeworkNotFound(id);

    /// <summary>
    /// Marks homework done (no more reminders, gone from the bot and the practice page) or active again.
    /// </summary>
    /// <param name="id">Homework identifier.</param>
    /// <param name="request">The status.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The homework.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no homework with this identifier.</response>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType<HomeworkDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<HomeworkDto>> SetStatus(Guid id, SetHomeworkStatusRequest request, CancellationToken cancellationToken) =>
        await homeworkService.SetStatusAsync(id, request.Status!.Value, cancellationToken) is { } homework ? homework : HomeworkNotFound(id);

    /// <summary>
    /// Deletes homework with its practice marks.
    /// </summary>
    /// <param name="id">Homework identifier.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no homework with this identifier.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await homeworkService.DeleteAsync(id, cancellationToken) ? NoContent() : HomeworkNotFound(id);

    private ObjectResult HomeworkNotFound(Guid id) =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Homework not found", detail: $"Homework '{id}' does not exist.");
}
