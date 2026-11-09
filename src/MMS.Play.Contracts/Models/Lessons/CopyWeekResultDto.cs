namespace MMS.Play.Contracts.Models.Lessons;

/// <summary>
/// What copying the previous week's lessons into a week did.
/// </summary>
/// <param name="Created">The new lessons, by date and time.</param>
/// <param name="Skipped">Lessons of the previous week that were not copied, and why.</param>
public record CopyWeekResultDto(IReadOnlyList<LessonDto> Created, IReadOnlyList<SkippedLessonCopyDto> Skipped);

/// <summary>
/// A lesson of the previous week that was not copied.
/// </summary>
/// <param name="Date">The day it would have been on, in the target week.</param>
/// <param name="StartTime">Its start.</param>
/// <param name="Title">Student name, or the note of a lesson without a student.</param>
/// <param name="Reason">Why it was not copied.</param>
/// <param name="Detail">For <see cref="CopySkipReason.Clash"/>: what it clashes with, in words for the admin.</param>
public record SkippedLessonCopyDto(DateOnly Date, TimeOnly StartTime, string? Title, CopySkipReason Reason, string? Detail);

public enum CopySkipReason
{
    /// <summary>
    /// The same lesson (time, room, teacher, student) is already in the week, also when cancelled or rescheduled there.
    /// </summary>
    AlreadyExists,

    /// <summary>
    /// Its time in the target week has already come, e.g. Monday when copying on Wednesday.
    /// </summary>
    InThePast,

    /// <summary>
    /// The room, the teacher or the student is busy at that time.
    /// </summary>
    Clash
}
