namespace MMS.Play.Contracts.Models.Lessons;

/// <summary>
/// One lesson in a room at a time. Dates and times are the school's local time (see the "Lessons:TimeZoneId" setting).
/// </summary>
/// <param name="Id">Lesson identifier.</param>
/// <param name="Date">Day of the lesson.</param>
/// <param name="StartTime">Start, the lesson lasts <see cref="DurationMinutes"/> minutes.</param>
/// <param name="RoomId">Room identifier, see <see cref="RoomDto"/>.</param>
/// <param name="TeacherId">Teacher identifier, see <see cref="TeacherDto"/>.</param>
/// <param name="StudentId">Telegram subscriber who attends, null for a lesson described by <paramref name="Note"/> only (a trial lesson).</param>
/// <param name="StudentName">First and last name of the student.</param>
/// <param name="StudentIsSubscribed">Whether the student is subscribed in the bot, i.e. can get reminders.</param>
/// <param name="Note">Free text shown with the lesson, e.g. "online" or "trial, guitar".</param>
/// <param name="Price">Price of this lesson, copied from the default price when it was created.</param>
/// <param name="IsPaid">The student has paid for this lesson.</param>
/// <param name="PaidAt">When it was marked as paid (UTC).</param>
/// <param name="Status">Rescheduled lessons stay where they were, crossed out, and count nowhere.</param>
/// <param name="RescheduledToId">The lesson that replaced this one, set when <paramref name="Status"/> is Rescheduled.</param>
/// <param name="PendingReschedule">A time the student proposed in the bot that waits for an admin.</param>
/// <param name="Cancellation">Who cancelled the lesson, when and why; set when <paramref name="Status"/> is Cancelled.</param>
/// <param name="SeriesId">The weekly series the lesson belongs to, null for a one-off lesson; see <see cref="LessonChangeScope"/>.</param>
public record LessonDto(
    Guid Id,
    DateOnly Date,
    TimeOnly StartTime,
    Guid RoomId,
    Guid TeacherId,
    Guid? StudentId,
    string? StudentName,
    bool? StudentIsSubscribed,
    string? Note,
    decimal Price,
    bool IsPaid,
    DateTime? PaidAt,
    LessonStatus Status,
    Guid? RescheduledToId,
    PendingRescheduleDto? PendingReschedule,
    LessonCancellationDto? Cancellation,
    Guid? SeriesId)
{
    public const int DurationMinutes = 60;
    public const int NoteMaxLength = 100;
    public const int MaxRepeatWeeks = 26;
    public const decimal MaxPrice = 1_000_000;

    /// <summary>
    /// Lessons start between these times, so a lesson never crosses midnight.
    /// </summary>
    public static readonly TimeOnly EarliestStart = new(7, 0);

    public static readonly TimeOnly LatestStart = new(22, 0);
}
