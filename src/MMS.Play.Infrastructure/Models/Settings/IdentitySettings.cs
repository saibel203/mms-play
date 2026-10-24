using System.ComponentModel.DataAnnotations;

namespace MMS.Play.Infrastructure.Models.Settings;

/// <summary>
/// Bound from the "Identity" section in appsettings.
/// </summary>
public class IdentitySettings
{
    public const string SectionName = "Identity";

    /// <summary>
    /// Accounts created on startup when they do not exist yet. Existing accounts keep their password,
    /// only a missing role is added. Keep real passwords in user secrets or environment variables, not in appsettings.
    /// </summary>
    public SeedUserSettings[] SeedUsers { get; set; } = [];
}

public class SeedUserSettings
{
    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public string? Email { get; set; }

    /// <summary>
    /// One of MMS.Play.Contracts.Authorization.Roles.
    /// </summary>
    [Required]
    public string Role { get; set; } = string.Empty;
}
