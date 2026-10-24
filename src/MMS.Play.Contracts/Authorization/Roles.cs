namespace MMS.Play.Contracts.Authorization;

/// <summary>
/// Role names stored in the database and checked by [Authorize(Roles = ...)].
/// </summary>
public static class Roles
{
    /// <summary>
    /// Manages scores, sees jobs, health and logs.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Browses and plays scores, read only.
    /// </summary>
    public const string Student = "Student";

    public static IReadOnlyList<string> All { get; } = [Admin, Student];
}
