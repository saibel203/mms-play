using Microsoft.EntityFrameworkCore;
using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Lessons;

/// <summary>
/// A cancelled lesson, with everything the messages about it need.
/// </summary>
public sealed record CancelledLessonInfo(
    Guid LessonId,
    Guid? StudentId,
    string? StudentName,
    DateOnly Date,
    TimeOnly StartTime,
    string RoomName,
    string TeacherName,
    CancellationSource By,
    string? ByName,
    string? Reason,
    bool IsLate);

/// <summary>
/// Why a lesson cannot be cancelled.
/// </summary>
public enum CancellationProblem
{
    /// <summary>
    /// Deleted, or the user is neither its student nor a parent of the student.
    /// </summary>
    NotFound,

    /// <summary>
    /// Already cancelled or rescheduled, or already started.
    /// </summary>
    NotCancellable
}

/// <summary>
/// Cancels lessons (an admin on the website, the student or a parent in the bot) and restores them.
/// A cancelled lesson keeps its place, crossed out, and counts nowhere; its pending reschedule request is closed.
/// The people concerned are told through <see cref="ICancellationNotifier"/>: the family when an admin cancels,
/// the bot admins when the family does.
/// </summary>
public class LessonCancellationService(ApplicationDbContext context, SchoolClock clock, ICancellationNotifier notifier)
{
    /// <returns>The cancelled lesson, or why it could not be cancelled.</returns>
    public async Task<(CancelledLessonInfo? Lesson, CancellationProblem? Problem)> CancelAsync(
        Guid lessonId,
        CancellationSource by,
        string? byName,
        string? reason,
        CancellationToken cancellationToken)
    {
        var lesson = await context.Lessons
            .Include(x => x.Student)
            .Include(x => x.Room)
            .Include(x => x.Teacher)
            .Include(x => x.RescheduleRequests.Where(request => request.Status == RescheduleRequestStatus.Pending))
            .FirstOrDefaultAsync(x => x.Id == lessonId, cancellationToken);

        if (lesson is null)
            return (null, CancellationProblem.NotFound);

        var now = clock.Now;
        var start = lesson.Date.ToDateTime(lesson.StartTime);
        if (lesson.Status != LessonStatus.Scheduled || start <= now)
            return (null, CancellationProblem.NotCancellable);

        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (reason?.Length > LessonCancellationDto.ReasonMaxLength)
            reason = reason[..LessonCancellationDto.ReasonMaxLength];

        lesson.Status = LessonStatus.Cancelled;
        lesson.CancelledAtUtc = clock.UtcNow;
        lesson.CancelledBy = by;
        lesson.CancelledByName = byName is { Length: > 128 } ? byName[..128] : byName;
        lesson.CancellationReason = reason;
        lesson.IsLateCancellation = start - now < TimeSpan.FromHours(LessonCancellationDto.LateHours);

        // a time proposed for a lesson that will not take place is moot
        foreach (var request in lesson.RescheduleRequests)
        {
            request.Status = RescheduleRequestStatus.Cancelled;
            request.DecidedAtUtc = clock.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);

        var info = new CancelledLessonInfo(
            lesson.Id,
            lesson.StudentId,
            lesson.Student is null ? null : FullName(lesson.Student),
            lesson.Date,
            lesson.StartTime,
            lesson.Room.Name,
            lesson.Teacher.Name,
            by,
            lesson.CancelledByName,
            reason,
            lesson.IsLateCancellation);

        // the cancellation is stored; a message that does not go out is logged by the notifier
        if (by == CancellationSource.Admin)
            await notifier.NotifyFamilyAsync(info, CancellationToken.None);
        else
            await notifier.NotifyAdminsAsync(info, CancellationToken.None);

        return (info, null);
    }

    /// <summary>
    /// Brings a cancelled lesson back, when its time is still free.
    /// </summary>
    /// <returns>False when the lesson does not exist.</returns>
    /// <exception cref="ValidationFailedException">Not cancelled, or the room, teacher or student is busy by now.</exception>
    public async Task<bool> RestoreAsync(Guid lessonId, CancellationToken cancellationToken)
    {
        var lesson = await context.Lessons.FirstOrDefaultAsync(x => x.Id == lessonId, cancellationToken);
        if (lesson is null)
            return false;

        if (lesson.Status != LessonStatus.Cancelled)
            throw new ValidationFailedException(nameof(LessonDto.Status), "Only a cancelled lesson can be restored.");

        var clash = await LessonClashes.FindAsync(
            context, lesson.Id, lesson.Date, lesson.StartTime, lesson.RoomId, lesson.TeacherId, lesson.StudentId, cancellationToken);
        if (clash is not null)
            throw new ValidationFailedException(nameof(LessonDto.Status), $"Cannot restore: {clash}");

        lesson.Status = LessonStatus.Scheduled;
        lesson.CancelledAtUtc = null;
        lesson.CancelledBy = null;
        lesson.CancelledByName = null;
        lesson.CancellationReason = null;
        lesson.IsLateCancellation = false;

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// The student of the lesson, or a parent of the student, may cancel it in the bot.
    /// </summary>
    /// <returns>Student or Parent, null when the user may not.</returns>
    public async Task<(CancellationSource Source, string Name)?> WhoMayCancelAsync(Guid lessonId, long telegramUserId, CancellationToken cancellationToken)
    {
        var lesson = await context.Lessons
            .AsNoTracking()
            .Where(x => x.Id == lessonId && x.Student != null)
            .Select(x => new
            {
                StudentUserId = x.Student!.TelegramUserId,
                StudentName = x.Student.FirstName,
                Parent = x.Student.Parents
                    .Where(parent => parent.IsParent && parent.TelegramUserId == telegramUserId)
                    .Select(parent => parent.FirstName)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (lesson is null)
            return null;
        if (lesson.Parent is { } parentName)
            return (CancellationSource.Parent, parentName);
        if (lesson.StudentUserId == telegramUserId)
            return (CancellationSource.Student, lesson.StudentName);
        return null;
    }

    private static string FullName(TelegramSubscriber subscriber) =>
        subscriber.LastName is null ? subscriber.FirstName : $"{subscriber.FirstName} {subscriber.LastName}";
}
