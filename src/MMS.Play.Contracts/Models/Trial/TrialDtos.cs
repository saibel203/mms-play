namespace MMS.Play.Contracts.Models.Trial;

/// <summary>
/// Free times for a trial lesson, school time.
/// </summary>
/// <param name="IsOpen">False when trial booking is turned off in the notification settings: no days then.</param>
/// <param name="Days">Days with at least one free time, in order.</param>
public record TrialSlotsDto(bool IsOpen, IReadOnlyList<TrialDayDto> Days);

/// <param name="Date">The day.</param>
/// <param name="Times">Lesson starts that are free: an open room and a free teacher.</param>
public record TrialDayDto(DateOnly Date, IReadOnlyList<TimeOnly> Times);

/// <summary>
/// A request sent from the public page.
/// </summary>
/// <param name="Name">Who comes to the lesson.</param>
/// <param name="Contact">Phone or @telegram, how the school reaches them.</param>
/// <param name="Comment">Instrument, age, experience...</param>
/// <param name="Date">The preferred day, with <paramref name="Time"/>; both null = "any time, call me".</param>
/// <param name="Time">The preferred start.</param>
public record TrialRequestInput(string Name, string Contact, string? Comment, DateOnly? Date, TimeOnly? Time)
{
    public const int NameMaxLength = 100;
    public const int ContactMaxLength = 100;
    public const int CommentMaxLength = 500;
}

/// <summary>
/// A trial lesson request for the admin page.
/// </summary>
/// <param name="Id">Request identifier.</param>
/// <param name="Source">The bot or the public page.</param>
/// <param name="SubscriberId">The bot subscriber who asked, null for the public page.</param>
/// <param name="Name">Who comes.</param>
/// <param name="Contact">Phone or @telegram.</param>
/// <param name="Comment">What they wrote.</param>
/// <param name="PreferredDate">The day they picked, null for "any time".</param>
/// <param name="PreferredTime">The time they picked.</param>
/// <param name="Status">New, booked or declined.</param>
/// <param name="LessonId">The lesson created when booked.</param>
/// <param name="LessonDate">Day of that lesson.</param>
/// <param name="LessonStartTime">Start of that lesson.</param>
/// <param name="CreatedAt">When the request came (UTC).</param>
/// <param name="DecidedAt">When an admin booked or declined it (UTC).</param>
public record TrialRequestDto(
    Guid Id,
    TrialRequestSource Source,
    Guid? SubscriberId,
    string Name,
    string? Contact,
    string? Comment,
    DateOnly? PreferredDate,
    TimeOnly? PreferredTime,
    TrialRequestStatus Status,
    Guid? LessonId,
    DateOnly? LessonDate,
    TimeOnly? LessonStartTime,
    DateTime CreatedAt,
    DateTime? DecidedAt);

/// <summary>
/// Where and when the trial lesson takes place; the price defaults to the default lesson price.
/// </summary>
public record TrialBookingInput(Guid TeacherId, Guid RoomId, DateOnly Date, TimeOnly StartTime, decimal? Price);
