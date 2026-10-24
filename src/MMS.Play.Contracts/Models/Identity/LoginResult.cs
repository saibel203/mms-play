using System.Diagnostics.CodeAnalysis;

namespace MMS.Play.Contracts.Models.Identity;

/// <param name="Status">Outcome of the sign-in.</param>
/// <param name="User">The signed-in user, set only when <paramref name="Status"/> is <see cref="LoginStatus.Succeeded"/>.</param>
public record LoginResult(LoginStatus Status, CurrentUserDto? User = null)
{
    [MemberNotNullWhen(true, nameof(User))]
    public bool Succeeded => Status == LoginStatus.Succeeded;
}
