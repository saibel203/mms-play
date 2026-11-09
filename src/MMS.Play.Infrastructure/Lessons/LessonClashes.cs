using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Infrastructure.Persistence;

namespace MMS.Play.Infrastructure.Lessons;

/// <summary>
/// A room, a teacher and a student are each in one lesson at a time: another lesson starting less than
/// <see cref="LessonDto.DurationMinutes"/> before or after is a clash. Rescheduled lessons no longer take place
/// and never clash.
/// </summary>
public static class LessonClashes
{
    /// <summary>
    /// What the lesson would clash with, in words for the admin; null when the time is free.
    /// </summary>
    /// <param name="context">Database.</param>
    /// <param name="exceptLessonId">The lesson being moved or edited, it does not clash with itself.</param>
    /// <param name="date">Day of the lesson.</param>
    /// <param name="startTime">Start, between <see cref="LessonDto.EarliestStart"/> and <see cref="LessonDto.LatestStart"/>.</param>
    /// <param name="roomId">Room of the lesson.</param>
    /// <param name="teacherId">Teacher of the lesson.</param>
    /// <param name="studentId">Student of the lesson, if any.</param>
    /// <param name="cancellationToken">Aborts the query.</param>
    public static Task<string?> FindAsync(
        ApplicationDbContext context,
        Guid? exceptLessonId,
        DateOnly date,
        TimeOnly startTime,
        Guid roomId,
        Guid teacherId,
        Guid? studentId,
        CancellationToken cancellationToken) =>
        FindAsync(context, exceptLessonId is { } id ? [id] : [], date, startTime, roomId, teacherId, studentId, cancellationToken);

    /// <inheritdoc cref="FindAsync(ApplicationDbContext, Guid?, DateOnly, TimeOnly, Guid, Guid, Guid?, CancellationToken)"/>
    /// <param name="exceptLessonIds">The lessons being moved together (a series), they do not clash with each other's old places.</param>
    public static async Task<string?> FindAsync(
        ApplicationDbContext context,
        IReadOnlyCollection<Guid> exceptLessonIds,
        DateOnly date,
        TimeOnly startTime,
        Guid roomId,
        Guid teacherId,
        Guid? studentId,
        CancellationToken cancellationToken)
    {
        // no wrap around midnight: start times are limited to EarliestStart..LatestStart
        var after = startTime.AddMinutes(-LessonDto.DurationMinutes);
        var before = startTime.AddMinutes(LessonDto.DurationMinutes);

        var clash = await context.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.Date == date
                             && lesson.Status == LessonStatus.Scheduled
                             && !exceptLessonIds.Contains(lesson.Id)
                             && lesson.StartTime > after
                             && lesson.StartTime < before)
            .Where(lesson => lesson.RoomId == roomId
                             || lesson.TeacherId == teacherId
                             || (studentId != null && lesson.StudentId == studentId))
            .OrderBy(lesson => lesson.StartTime)
            .Select(lesson => new
            {
                lesson.StartTime,
                lesson.RoomId,
                lesson.TeacherId,
                Room = lesson.Room.Name,
                Teacher = lesson.Teacher.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (clash is null)
            return null;

        var when = $"{date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)} at {clash.StartTime:HH\\:mm}";
        return clash.RoomId == roomId
            ? $"Room {clash.Room} is taken on {when}."
            : clash.TeacherId == teacherId
                ? $"{clash.Teacher} already teaches on {when} (room {clash.Room})."
                : $"The student already has a lesson on {when} (room {clash.Room}).";
    }
}
