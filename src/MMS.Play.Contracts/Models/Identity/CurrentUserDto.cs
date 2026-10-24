namespace MMS.Play.Contracts.Models.Identity;

/// <summary>
/// The signed-in user.
/// </summary>
/// <param name="Id">User identifier.</param>
/// <param name="UserName">Login name.</param>
/// <param name="Roles">Admin and/or Student, decides which parts of the client are available.</param>
public record CurrentUserDto(Guid Id, string UserName, IReadOnlyList<string> Roles);
