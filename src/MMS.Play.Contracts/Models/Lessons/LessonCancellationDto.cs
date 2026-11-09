namespace MMS.Play.Contracts.Models.Lessons;

/// <summary>
/// Who cancelled a lesson.
/// </summary>
public enum CancellationSource
{
    /// <summary>
    /// An admin on the website.
    /// </summary>
    Admin,

    /// <summary>
    /// The student in the bot.
    /// </summary>
    Student,

    /// <summary>
    /// A parent of the student in the bot.
    /// </summary>
    Parent
}

/// <summary>
/// How a cancelled lesson was cancelled.
/// </summary>
/// <param name="CancelledAt">When (UTC).</param>
/// <param name="By">Who: an admin, the student or a parent.</param>
/// <param name="ByName">The name of who cancelled.</param>
/// <param name="Reason">Why, as picked in the bot or written by the admin.</param>
/// <param name="IsLate">Cancelled less than <see cref="LateHours"/> hours before the start.</param>
public record LessonCancellationDto(DateTime CancelledAt, CancellationSource By, string? ByName, string? Reason, bool IsLate)
{
    /// <summary>
    /// A cancellation closer to the start than this is late: the time can rarely be given to someone else.
    /// </summary>
    public const int LateHours = 24;

    public const int ReasonMaxLength = 200;
}
