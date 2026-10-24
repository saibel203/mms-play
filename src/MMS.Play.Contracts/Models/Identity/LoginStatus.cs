namespace MMS.Play.Contracts.Models.Identity;

public enum LoginStatus
{
    Succeeded,

    /// <summary>
    /// Unknown user name or wrong password, deliberately not told apart.
    /// </summary>
    InvalidCredentials,

    /// <summary>
    /// Too many failed attempts, sign-in is blocked for a while.
    /// </summary>
    LockedOut,

    /// <summary>
    /// The account exists but may not sign in (e.g. a confirmation is required).
    /// </summary>
    NotAllowed
}
