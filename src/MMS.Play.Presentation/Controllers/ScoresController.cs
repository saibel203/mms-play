using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Scores;
using MMS.Play.Contracts.Models.Common;
using MMS.Play.Contracts.Models.Scores;
using MMS.Play.Presentation.Controllers.Base;
using MMS.Play.Presentation.Models.Requests.Scores;

namespace MMS.Play.Presentation.Controllers;

/// <summary>
/// Guitar Pro and MusicXML files played by the tab player.
/// </summary>
[ApiVersion(1)]
public class ScoresController(IScoreService scoreService) : BaseController
{
    // the file of a score never changes, so the browser can keep it until the cache is cleared
    private const string ContentCacheControl = "private, max-age=31536000, immutable";

    /// <summary>
    /// Uploads a score file.
    /// </summary>
    /// <remarks>
    /// Supported formats: Guitar Pro 3-5 (.gp3, .gp4, .gp5), Guitar Pro 6 (.gpx), Guitar Pro 7+ (.gp) and MusicXML (.musicxml, .xml).
    /// The file content is checked against its extension.
    ///
    /// A file with the same content is stored once: uploading it again returns the existing score with 200 instead of 201.
    /// </remarks>
    /// <response code="201">The score was stored, the Location header points to it.</response>
    /// <response code="200">The same file had already been uploaded, the existing score is returned.</response>
    /// <response code="400">The file is missing, empty, too large, of an unsupported type or its content does not match the extension.</response>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ScoreDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ScoreDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Upload([FromForm] UploadScoreRequest request, CancellationToken cancellationToken)
    {
        await using var content = request.File.OpenReadStream();

        var upload = new ScoreUpload(content, request.File.FileName, request.File.Length, request.Title, request.Artist);
        var (score, isDuplicate) = await scoreService.UploadAsync(upload, cancellationToken);

        if (isDuplicate)
            return Ok(score);

        // link generation drops ambient route values when the target action differs, so the version segment is passed as requested
        var routeValues = new { id = score.Id, version = RouteData.Values["version"] };
        return CreatedAtAction(nameof(Get), routeValues, score);
    }

    /// <summary>
    /// Lists uploaded scores, newest first.
    /// </summary>
    /// <response code="200">The requested page, empty when it is past the last one.</response>
    /// <response code="400">Invalid paging or search parameters.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<ScoreDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PagedResult<ScoreDto>> List([FromQuery] ListScoresRequest request, CancellationToken cancellationToken) =>
        scoreService.ListAsync(new ScoreListQuery(request.Page, request.PageSize, request.Search), cancellationToken);

    /// <summary>
    /// Returns the score metadata.
    /// </summary>
    /// <param name="id">Score identifier.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The score.</response>
    /// <response code="404">There is no score with this identifier.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ScoreDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ScoreDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var score = await scoreService.GetAsync(id, cancellationToken);
        if (score is null)
            return ScoreNotFound(id);

        return score;
    }

    /// <summary>
    /// Downloads the score file, the response body can be passed to alphaTab as is.
    /// </summary>
    /// <remarks>
    /// Supports conditional (If-None-Match, If-Modified-Since) and Range requests.
    /// The file of a score never changes, so the response is cached by the browser.
    /// </remarks>
    /// <param name="id">Score identifier.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The file.</response>
    /// <response code="206">The requested range of the file.</response>
    /// <response code="304">The cached copy is still valid.</response>
    /// <response code="404">There is no score with this identifier.</response>
    [HttpGet("{id:guid}/content")]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK, "application/octet-stream", "application/vnd.recordare.musicxml+xml", "application/xml")]
    [ProducesResponseType(StatusCodes.Status206PartialContent)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetContent(Guid id, CancellationToken cancellationToken)
    {
        var content = await scoreService.OpenContentAsync(id, cancellationToken);
        if (content is null)
            return ScoreNotFound(id);

        Response.Headers.CacheControl = ContentCacheControl;

        // FileStreamResult disposes the stream; a matching If-None-Match ends in 304 without reading it
        return File(
            content.Content,
            content.ContentType,
            content.FileName,
            lastModified: content.UploadedAt,
            entityTag: new EntityTagHeaderValue($"\"{content.Sha256}\""),
            enableRangeProcessing: true);
    }

    /// <summary>
    /// Deletes the score and its file.
    /// </summary>
    /// <remarks>
    /// Browsers that have already downloaded the file may keep serving it from their cache.
    /// </remarks>
    /// <param name="id">Score identifier.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="204">The score was deleted.</response>
    /// <response code="404">There is no score with this identifier.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await scoreService.DeleteAsync(id, cancellationToken) ? NoContent() : ScoreNotFound(id);

    private ObjectResult ScoreNotFound(Guid id) =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Score not found", detail: $"Score '{id}' does not exist.");
}
