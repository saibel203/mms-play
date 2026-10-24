using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MMS.Play.Contracts.Authorization;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Identity;

/// <summary>
/// Creates the roles from <see cref="Roles"/> and the accounts from "Identity:SeedUsers". Safe to run on every startup.
/// </summary>
public partial class IdentitySeeder(
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<User> userManager,
    IOptions<IdentitySettings> options,
    ILogger<IdentitySeeder> logger)
{
    public async Task SeedAsync()
    {
        foreach (var role in Roles.All)
        {
            if (await roleManager.RoleExistsAsync(role))
                continue;

            EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole<Guid>(role)), $"create role '{role}'");
            LogRoleCreated(logger, role);
        }

        foreach (var seedUser in options.Value.SeedUsers)
        {
            var user = await userManager.FindByNameAsync(seedUser.UserName);
            if (user is null)
            {
                user = new User { UserName = seedUser.UserName, Email = seedUser.Email };
                EnsureSucceeded(await userManager.CreateAsync(user, seedUser.Password), $"create user '{seedUser.UserName}'");
                LogUserCreated(logger, seedUser.UserName, seedUser.Role);
            }

            if (!await userManager.IsInRoleAsync(user, seedUser.Role))
                EnsureSucceeded(await userManager.AddToRoleAsync(user, seedUser.Role), $"add user '{seedUser.UserName}' to role '{seedUser.Role}'");
        }
    }

    // a failed seed (e.g. a password that breaks the policy) must stop the startup instead of leaving the app without an admin
    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not {operation}: {string.Join(" ", result.Errors.Select(error => error.Description))}");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Role {Role} created")]
    private static partial void LogRoleCreated(ILogger logger, string role);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserName} created with role {Role}")]
    private static partial void LogUserCreated(ILogger logger, string userName, string role);
}
