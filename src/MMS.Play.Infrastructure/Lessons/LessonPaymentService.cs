using Microsoft.EntityFrameworkCore;
using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Lessons;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Infrastructure.Persistence;

namespace MMS.Play.Infrastructure.Lessons;

public class LessonPaymentService(ApplicationDbContext context, SchoolClock clock) : ILessonPaymentService
{
    public const string WithoutStudentName = "Without a student";

    public async Task<PaymentOverviewDto> GetOverviewAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        if (from > to)
            throw new ValidationFailedException("From", "The first day is after the last one.");

        var now = clock.Now;
        var today = DateOnly.FromDateTime(now);
        var nowTime = TimeOnly.FromDateTime(now);

        var query = context.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.Status == LessonStatus.Scheduled);
        if (from is { } first)
            query = query.Where(lesson => lesson.Date >= first);
        if (to is { } last)
            query = query.Where(lesson => lesson.Date <= last);

        var lessons = await query
            .OrderBy(lesson => lesson.Date)
            .ThenBy(lesson => lesson.StartTime)
            .Select(lesson => new
            {
                lesson.StudentId,
                Lesson = new PaymentLessonDto(
                    lesson.Id,
                    lesson.Date,
                    lesson.StartTime,
                    lesson.Teacher.Name,
                    lesson.Teacher.Color,
                    lesson.Room.Name,
                    lesson.Note,
                    lesson.Price,
                    lesson.IsPaid,
                    lesson.PaidAtUtc,
                    false)
            })
            .ToListAsync(cancellationToken);

        var studentIds = lessons.Where(x => x.StudentId != null).Select(x => x.StudentId!.Value).Distinct().ToList();

        var students = await context.TelegramSubscribers
            .AsNoTracking()
            .Where(subscriber => studentIds.Contains(subscriber.Id))
            .Select(subscriber => new
            {
                subscriber.Id,
                Name = subscriber.LastName == null ? subscriber.FirstName : subscriber.FirstName + " " + subscriber.LastName,
                subscriber.Username,
                subscriber.IsActive,
                Parents = subscriber.Parents.Where(parent => parent.IsParent).Select(parent => parent.FirstName).ToList()
            })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var lastReminders = await context.PaymentReminderDeliveries
            .AsNoTracking()
            .Where(delivery => studentIds.Contains(delivery.StudentId))
            .GroupBy(delivery => delivery.StudentId)
            .Select(group => new { StudentId = group.Key, SentAt = group.Max(delivery => delivery.SentAtUtc) })
            .ToDictionaryAsync(x => x.StudentId, x => DateTime.SpecifyKind(x.SentAt, DateTimeKind.Utc), cancellationToken);

        var rows = lessons
            .GroupBy(x => x.StudentId)
            .Select(group =>
            {
                var items = group
                    .Select(x => x.Lesson with { IsDue = IsDue(x.Lesson, today, nowTime) })
                    .ToList();
                var student = group.Key is { } id ? students.GetValueOrDefault(id) : null;
                var due = items.Where(lesson => lesson.IsDue).ToList();
                var upcoming = items.Where(lesson => !lesson.IsPaid && !lesson.IsDue).ToList();
                var paid = items.Where(lesson => lesson.IsPaid).ToList();

                return new StudentPaymentsDto(
                    group.Key,
                    student?.Name ?? WithoutStudentName,
                    student?.Username,
                    student?.IsActive ?? false,
                    student?.Parents ?? [],
                    items.Count,
                    paid.Count,
                    paid.Sum(lesson => lesson.Price),
                    due.Count,
                    due.Sum(lesson => lesson.Price),
                    upcoming.Count,
                    upcoming.Sum(lesson => lesson.Price),
                    group.Key is { } studentId && lastReminders.TryGetValue(studentId, out var sentAt) ? sentAt : null,
                    items);
            })
            .OrderBy(row => row.StudentId is null)
            .ThenByDescending(row => row.DueAmount)
            .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var totals = new PaymentTotalsDto(
            rows.Sum(row => row.LessonCount),
            rows.Sum(row => row.PaidAmount),
            rows.Sum(row => row.DueAmount),
            rows.Sum(row => row.UpcomingUnpaidAmount),
            rows.Count(row => row.StudentId is not null && row.DueAmount > 0));

        return new PaymentOverviewDto(from, to, today, rows, totals);
    }

    public async Task<int> SetPaidAsync(IReadOnlyList<Guid> lessonIds, bool isPaid, CancellationToken cancellationToken = default)
    {
        var ids = (lessonIds ?? []).Distinct().ToList();
        if (ids.Count is 0 or > ILessonPaymentService.MaxBulkLessons)
            throw new ValidationFailedException("LessonIds", $"Give 1 to {ILessonPaymentService.MaxBulkLessons} lessons.");

        var paidAt = isPaid ? clock.UtcNow : (DateTime?)null;

        // only the ones that change: a lesson paid earlier keeps its payment time
        return await context.Lessons
            .Where(lesson => ids.Contains(lesson.Id) && lesson.Status == LessonStatus.Scheduled && lesson.IsPaid != isPaid)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(lesson => lesson.IsPaid, isPaid)
                .SetProperty(lesson => lesson.PaidAtUtc, paidAt), cancellationToken);
    }

    /// <summary>
    /// Started and not paid. A free lesson (price 0) is never due.
    /// </summary>
    public static bool IsDue(PaymentLessonDto lesson, DateOnly today, TimeOnly now) =>
        !lesson.IsPaid && lesson.Price > 0 && (lesson.Date < today || (lesson.Date == today && lesson.StartTime <= now));
}
