using MMS.Play.Contracts.Models.Identity;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Identity;

/// <summary>
/// Cookie sign-in of the current HTTP request.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Checks the password and issues the authentication cookie. Repeated failures lock the account for a few minutes.
    /// </summary>
    /// <param name="userName">User name, case insensitive.</param>
    /// <param name="password">Plain text password.</param>
    /// <param name="rememberMe">Persistent cookie that survives closing the browser, otherwise a session cookie.</param>
    Task<LoginResult> LoginAsync(string userName, string password, bool rememberMe);

    /// <summary>
    /// Removes the authentication cookie, does nothing when the user is not signed in.
    /// </summary>
    Task LogoutAsync();
}
