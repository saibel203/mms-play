using Microsoft.AspNetCore.Identity;

namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// ASP.NET Core Identity user, stored in the AspNetUsers table. Roles are assigned through AspNetUserRoles.
/// </summary>
public class User : IdentityUser<Guid>;
