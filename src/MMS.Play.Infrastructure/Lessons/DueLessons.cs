using Microsoft.EntityFrameworkCore;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Infrastructure.Persistence;

namespace MMS.Play.Infrastructure.Lessons;

/// <summary>
/// A lesson of a student that took place and is not paid.
/// </summary>
public sealed record DueLesson(Guid StudentId, DateOnly Date, TimeOnly StartTime, string Teacher, decimal Price);

/// <summary>
/// The lessons students owe for: started, not paid, not free, not rescheduled. Shared by the payment reminders and /payments.
/// </summary>
public static class DueLessons
{
    /// <param name="context">The database.</param>
    /// <param name="studentIds">These students only, everybody when null.</param>
    /// <param name="now">School time.</param>
    /// <param name="cancellationToken">Aborts the query.</param>
    public static Task<List<DueLesson>> FindAsync(
        ApplicationDbContext context,
        IReadOnlyCollection<Guid>? studentIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(now);
        var time = TimeOnly.FromDateTime(now);

        var lessons = context.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.StudentId != null
                             && lesson.Status == LessonStatus.Scheduled
                             && !lesson.IsPaid
                             && lesson.Price > 0
                             && (lesson.Date < today || (lesson.Date == today && lesson.StartTime <= time)));

        if (studentIds is not null)
            lessons = lessons.Where(lesson => studentIds.Contains(lesson.StudentId!.Value));

        return lessons
            .OrderBy(lesson => lesson.Date)
            .ThenBy(lesson => lesson.StartTime)
            .Select(lesson => new DueLesson(lesson.StudentId!.Value, lesson.Date, lesson.StartTime, lesson.Teacher.Name, lesson.Price))
            .ToListAsync(cancellationToken);
    }
}
