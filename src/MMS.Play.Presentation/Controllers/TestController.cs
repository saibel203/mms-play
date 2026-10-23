using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MMS.Play.Presentation.Controllers.Base;

namespace MMS.Play.Presentation.Controllers;

/// <summary>
/// Smoke-test endpoints.
/// </summary>
[ApiVersion(1)]
public class TestController : BaseController
{
    /// <summary>
    /// Returns a constant string to check that the API is reachable.
    /// </summary>
    /// <response code="200">The API is up.</response>
    [HttpGet("get")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        throw new InvalidOperationException("TestException");
        return Ok("Test");
    }
}
