using System.ComponentModel.DataAnnotations;

namespace MMS.Play.Presentation.Models.Requests.Account;

/// <summary>
/// Credentials of a sign-in.
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// Login name, case insensitive.
    /// </summary>
    [Required]
    [StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Keeps the user signed in after the browser is closed (14 days, renewed while the user is active).
    /// </summary>
    public bool RememberMe { get; set; }
}
