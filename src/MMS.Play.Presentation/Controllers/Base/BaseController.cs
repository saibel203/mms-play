using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MMS.Play.Presentation.Controllers.Base;

/// <summary>
/// Route, content type and the unhandled error response are inherited by every controller.
/// Mark derived controllers with [ApiVersion], unmarked ones fall back to the default version.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public class BaseController : ControllerBase;
