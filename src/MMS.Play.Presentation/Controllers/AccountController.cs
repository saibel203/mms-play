using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Identity;
using MMS.Play.Contracts.Models.Identity;
using MMS.Play.Presentation.Controllers.Base;
using MMS.Play.Presentation.Models.Requests.Account;

namespace MMS.Play.Presentation.Controllers;

/// <summary>
/// Cookie sign-in. The browser stores and sends the HttpOnly cookie by itself, the client only calls these endpoints.
/// </summary>
[ApiVersion(1)]
public class AccountController(IAuthService authService) : BaseController
{
    /// <summary>
    /// Signs in and sets the authentication cookie.
    /// </summary>
    /// <remarks>
    /// Five failed attempts in a row lock the account for five minutes.
    /// </remarks>
    /// <response code="200">Signed in, the current user is returned.</response>
    /// <response code="400">User name or password is missing.</response>
    /// <response code="401">Wrong credentials, the account is locked out or not allowed to sign in.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<CurrentUserDto>> Login(LoginRequest request)
    {
        var result = await authService.LoginAsync(request.UserName, request.Password, request.RememberMe);
        if (result.Succeeded)
            return result.User;

        var detail = result.Status switch
        {
            LoginStatus.LockedOut => "Too many failed attempts, try again in a few minutes.",
            LoginStatus.NotAllowed => "This account is not allowed to sign in.",
            _ => "Invalid user name or password."
        };

        return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign-in failed", detail: detail);
    }

    /// <summary>
    /// Signs out and removes the authentication cookie.
    /// </summary>
    /// <response code="204">Signed out, also returned when the user was not signed in.</response>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await authService.LogoutAsync();
        return NoContent();
    }

    /// <summary>
    /// Returns the signed-in user, call it on client startup to restore the session.
    /// </summary>
    /// <remarks>
    /// Available to signed-in users without a role as well, so the client can tell "not signed in" (401) from "no access".
    /// </remarks>
    /// <response code="200">The current user.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    public ActionResult<CurrentUserDto> Me() =>
        // read from the cookie, no database round trip; role changes reach it when Identity revalidates the cookie (every 30 minutes)
        new CurrentUserDto(
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            User.Identity!.Name!,
            [.. User.FindAll(ClaimTypes.Role).Select(claim => claim.Value)]);
}
