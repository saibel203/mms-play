using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.Models.Lessons;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Lessons;

/// <summary>
/// The lesson timetable: rooms, teachers and lessons with their payment.
/// </summary>
public interface ILessonService
{
    /// <summary>
    /// The week (Monday to Sunday) containing <paramref name="day"/>, the current week when null.
    /// </summary>
    Task<WeekScheduleDto> GetWeekAsync(DateOnly? day, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every Telegram subscriber by name, for picking the student of a lesson.
    /// </summary>
    Task<IReadOnlyList<StudentOptionDto>> ListStudentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the lesson, and the same lesson in the following weeks when <paramref name="repeatWeeks"/> is more than 1.
    /// Nothing is created when any of them clashes with another lesson.
    /// </summary>
    /// <exception cref="ValidationFailedException">Invalid values, an unknown room, teacher or student,
    /// or the room, the teacher or the student is busy at that time.</exception>
    Task<IReadOnlyList<LessonDto>> CreateAsync(LessonInput input, int repeatWeeks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies the lessons of the week before into the week (Monday to Sunday) containing <paramref name="day"/>:
    /// same day of the week, time, room, teacher, student, note and price, not paid.
    /// </summary>
    /// <remarks>
    /// The regular timetable is copied: a cancelled lesson is copied as scheduled, a rescheduled one at its original
    /// time, and the lesson that replaced it is left out. Lessons already in the week, whose time has come,
    /// or that would clash are skipped, so copying twice creates nothing the second time.
    /// </remarks>
    Task<CopyWeekResultDto> CopyPreviousWeekAsync(DateOnly day, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the lesson, with <see cref="LessonChangeScope.Following"/> also the later lessons of its series
    /// (all or nothing: nothing changes when one of them would clash).
    /// </summary>
    /// <returns>The updated lesson, null when it does not exist.</returns>
    /// <exception cref="ValidationFailedException">See <see cref="CreateAsync"/>.</exception>
    Task<LessonDto?> UpdateAsync(Guid id, LessonInput input, LessonChangeScope scope = LessonChangeScope.This,
        CancellationToken cancellationToken = default);

    /// <returns>The updated lesson, null when it does not exist.</returns>
    Task<LessonDto?> SetPaidAsync(Guid id, bool isPaid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the lesson, with <see cref="LessonChangeScope.Following"/> also the later lessons of its series from today on
    /// (any status, and the lessons they were rescheduled to), which ends the series. Past lessons keep their history.
    /// </summary>
    /// <returns>False when the lesson does not exist.</returns>
    Task<bool> DeleteAsync(Guid id, LessonChangeScope scope = LessonChangeScope.This, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a lesson that has not started; the student and the parents are told in the bot.
    /// </summary>
    /// <returns>The cancelled lesson, null when it does not exist.</returns>
    /// <exception cref="ValidationFailedException">Already cancelled, rescheduled or started.</exception>
    Task<LessonDto?> CancelAsync(Guid id, string? reason, string? cancelledBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Brings a cancelled lesson back.
    /// </summary>
    /// <returns>The lesson, null when it does not exist.</returns>
    /// <exception cref="ValidationFailedException">Not cancelled, or its time is taken by now.</exception>
    Task<LessonDto?> RestoreAsync(Guid id, CancellationToken cancellationToken = default);
}
