namespace MMS.Play.Contracts.Models.Notifications;

/// <summary>
/// Who would get a broadcast: a parent, and with <see cref="BroadcastAudience.LessonDay"/> the lesson it is about.
/// </summary>
/// <param name="ParentId">The parent's subscriber identifier.</param>
/// <param name="ParentName">First and last name.</param>
/// <param name="Username">Telegram username without "@".</param>
/// <param name="IsSubscribed">False: the parent unsubscribed or blocked the bot, a message would not arrive.</param>
/// <param name="ChildName">The child of the lesson, or all the parent's children; null when none is linked.</param>
/// <param name="Lesson">The lesson, null for <see cref="BroadcastAudience.AllParents"/>.</param>
public record BroadcastRecipientDto(
    Guid ParentId,
    string ParentName,
    string? Username,
    bool IsSubscribed,
    string? ChildName,
    BroadcastLessonDto? Lesson);

/// <param name="LessonId">Lesson identifier.</param>
/// <param name="Date">School time.</param>
/// <param name="StartTime">School time.</param>
/// <param name="Room">Room name.</param>
/// <param name="Teacher">Teacher name.</param>
public record BroadcastLessonDto(Guid LessonId, DateOnly Date, TimeOnly StartTime, string Room, string Teacher);
