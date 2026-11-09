using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Homework;
using MMS.Play.Contracts.Models.Homework;
using MMS.Play.Presentation.Controllers.Base;

namespace MMS.Play.Presentation.Controllers;

/// <summary>
/// The public practice page: a student (or parent) opens the personal link from the bot without signing in and sees
/// their active homework, plays its scores and marks practice. <c>token</c> is the secret of the link; an unknown or
/// replaced one is a 404, like homework that is not theirs.
/// </summary>
[ApiVersion(1)]
[AllowAnonymous]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
public class PracticeController(IPracticeService practiceService) : BaseController
{
    // the link is personal: the browser may keep the file, shared caches must not
    private const string ContentCacheControl = "private, max-age=86400";

    /// <summary>
    /// The link owner's name and active homework (their own and, for a parent, their children's), newest first.
    /// </summary>
    /// <param name="token">The secret of the personal link.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The homework.</response>
    /// <response code="404">Unknown link.</response>
    [HttpGet("{token}")]
    [ProducesResponseType<PracticeOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PracticeOverviewDto>> Get(string token, CancellationToken cancellationToken) =>
        await practiceService.GetOverviewAsync(token, cancellationToken) is { } overview ? overview : LinkNotFound();

    /// <summary>
    /// The score file of an active homework, to be passed to alphaTab as is.
    /// </summary>
    /// <param name="token">The secret of the personal link.</param>
    /// <param name="homeworkId">Homework identifier.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The file.</response>
    /// <response code="206">The requested range of the file.</response>
    /// <response code="404">Unknown link, homework that is not theirs or not active, or homework without a score.</response>
    [HttpGet("{token}/homework/{homeworkId:guid}/score")]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK, "application/octet-stream", "application/vnd.recordare.musicxml+xml", "application/xml")]
    [ProducesResponseType(StatusCodes.Status206PartialContent)]
    public async Task<IActionResult> GetScore(string token, Guid homeworkId, CancellationToken cancellationToken)
    {
        var content = await practiceService.OpenScoreAsync(token, homeworkId, cancellationToken);
        if (content is null)
            return LinkNotFound();

        Response.Headers.CacheControl = ContentCacheControl;

        // FileStreamResult disposes the stream; a matching If-None-Match ends in 304 without reading it
        return File(content.Content, content.ContentType, content.FileName, lastModified: content.UploadedAt, entityTag: new EntityTagHeaderValue($"\"{content.Sha256}\""), enableRangeProcessing: true);
    }

    /// <summary>
    /// Marks an active homework practised today (school date); again on the same day changes nothing.
    /// </summary>
    /// <param name="token">The secret of the personal link.</param>
    /// <param name="homeworkId">Homework identifier.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The homework with the new days-in-a-row count.</response>
    /// <response code="404">Unknown link, or homework that is not theirs or not active.</response>
    [HttpPost("{token}/homework/{homeworkId:guid}/practiced")]
    [ProducesResponseType<PracticeHomeworkDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PracticeHomeworkDto>> MarkPracticed(string token, Guid homeworkId, CancellationToken cancellationToken) =>
        await practiceService.MarkPracticedAsync(token, homeworkId, cancellationToken) is { } homework ? homework : LinkNotFound();

    private ObjectResult LinkNotFound() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: "The link is not valid (anymore), or the homework is not there.");
}
