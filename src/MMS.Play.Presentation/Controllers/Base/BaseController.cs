using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MMS.Play.Presentation.Controllers.Base;

/// <summary>
/// Route, content type and the unauthenticated/unhandled error responses are inherited by every controller.
/// Actions require a user with any role (fallback policy), admin-only actions add [Authorize(Roles = Roles.Admin)].
/// Mark derived controllers with [ApiVersion], unmarked ones fall back to the default version.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public class BaseController : ControllerBase;
