using Microsoft.AspNetCore.Identity;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Identity;
using MMS.Play.Contracts.Models.Identity;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Identity;

public class AuthService(SignInManager<User> signInManager) : IAuthService
{
    private static readonly LoginResult InvalidCredentials = new(LoginStatus.InvalidCredentials);

    public async Task<LoginResult> LoginAsync(string userName, string password, bool rememberMe)
    {
        var user = await signInManager.UserManager.FindByNameAsync(userName);
        if (user is null)
            return InvalidCredentials;

        var result = await signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return new LoginResult(LoginStatus.LockedOut);

        if (result.IsNotAllowed)
            return new LoginResult(LoginStatus.NotAllowed);

        if (!result.Succeeded)
            return InvalidCredentials;

        // HttpContext.User is not updated until the next request, so the response is built from the store
        var roles = await signInManager.UserManager.GetRolesAsync(user);
        return new LoginResult(LoginStatus.Succeeded, new CurrentUserDto(user.Id, user.UserName!, [.. roles]));
    }

    public Task LogoutAsync() => signInManager.SignOutAsync();
}
