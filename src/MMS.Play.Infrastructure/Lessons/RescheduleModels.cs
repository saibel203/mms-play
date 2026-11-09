namespace MMS.Play.Infrastructure.Lessons;

/// <summary>
/// A lesson as the reschedule flow shows it.
/// </summary>
public sealed record RescheduleLesson(
    Guid LessonId,
    string StudentName,
    string? StudentUsername,
    DateOnly Date,
    TimeOnly StartTime,
    string RoomName,
    string TeacherName);

/// <summary>
/// A time the teacher, the student and a room are all free; the room is the lesson's own one when possible.
/// </summary>
public sealed record FreeSlot(DateOnly Date, TimeOnly Time, Guid RoomId, string RoomName);

/// <summary>
/// What the student can pick from: free times, and the times an admin already declined (shown crossed out).
/// </summary>
public sealed record RescheduleOptions(
    RescheduleLesson Lesson,
    IReadOnlyList<FreeSlot> Slots,
    IReadOnlySet<(DateOnly Date, TimeOnly Time)> Declined,
    int DaysAhead);

/// <summary>
/// A proposal, with everything the messages to the student and the admins need.
/// </summary>
public sealed record RescheduleRequestInfo(
    Guid RequestId,
    RescheduleLesson Lesson,
    DateOnly ProposedDate,
    TimeOnly ProposedTime,
    string ProposedRoomName,
    long StudentChatId);

/// <summary>
/// An upcoming lesson of a student, for /lessons. <paramref name="ChildName"/> is set when a parent sees a child's lesson.
/// </summary>
public sealed record UpcomingLesson(
    Guid LessonId,
    DateOnly Date,
    TimeOnly StartTime,
    string RoomName,
    string TeacherName,
    bool HasPendingRequest,
    string? ChildName = null);

/// <summary>
/// Why a student cannot move this lesson (any more).
/// </summary>
public enum RescheduleProblem
{
    /// <summary>
    /// Deleted, not this student's, or the student is not known.
    /// </summary>
    NotFound,

    /// <summary>
    /// Already rescheduled or already started.
    /// </summary>
    NotMovable,

    /// <summary>
    /// A proposal for this lesson waits for an admin.
    /// </summary>
    AlreadyPending,

    /// <summary>
    /// The chosen time is no longer free (or was declined before).
    /// </summary>
    SlotTaken
}
