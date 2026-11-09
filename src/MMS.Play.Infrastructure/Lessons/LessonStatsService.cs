using Microsoft.EntityFrameworkCore;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Lessons;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Infrastructure.Persistence;

namespace MMS.Play.Infrastructure.Lessons;

public class LessonStatsService(ApplicationDbContext context, SchoolClock clock) : ILessonStatsService
{
    public async Task<LessonStatsDto> GetAsync(DateOnly? date, CancellationToken cancellationToken = default)
    {
        var day = date ?? clock.Today;
        var periods = GetPeriods(day);

        // one query for all periods: a week at the turn of the year sticks out of the year, so the range is their union
        var from = periods.Min(period => period.From);
        var to = periods.Max(period => period.To);

        var totals = await context.Lessons
            .AsNoTracking()
            // a rescheduled lesson took place elsewhere, as the lesson that replaced it
            .Where(lesson => lesson.Date >= from && lesson.Date <= to && lesson.Status == LessonStatus.Scheduled)
            .GroupBy(lesson => new { lesson.Date, lesson.TeacherId, lesson.IsPaid })
            .Select(group => new Total(group.Key.Date, group.Key.TeacherId, group.Key.IsPaid, group.Count(), group.Sum(lesson => lesson.Price)))
            .ToListAsync(cancellationToken);

        var cancelled = await context.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.Date >= from && lesson.Date <= to && lesson.Status == LessonStatus.Cancelled)
            .Select(lesson => new Cancelled(
                lesson.Date,
                lesson.TeacherId,
                lesson.StudentId,
                lesson.Student == null
                    ? lesson.Note ?? "—"
                    : lesson.Student.LastName == null ? lesson.Student.FirstName : lesson.Student.FirstName + " " + lesson.Student.LastName,
                lesson.IsLateCancellation,
                lesson.CancelledBy == CancellationSource.Admin))
            .ToListAsync(cancellationToken);

        var teachers = await context.Teachers
            .AsNoTracking()
            .OrderBy(teacher => teacher.SortOrder)
            .ThenBy(teacher => teacher.Name)
            .Select(teacher => new { teacher.Id, teacher.Name, teacher.Color })
            .ToListAsync(cancellationToken);

        var result = periods.Select(period =>
        {
            var inPeriod = totals.Where(total => total.Date >= period.From && total.Date <= period.To).ToList();
            var cancelledInPeriod = cancelled.Where(lesson => lesson.Date >= period.From && lesson.Date <= period.To).ToList();

            var byTeacher = teachers
                .Select(teacher =>
                {
                    var own = inPeriod.Where(total => total.TeacherId == teacher.Id).ToList();
                    return new TeacherStatsDto(
                        teacher.Id,
                        teacher.Name,
                        teacher.Color,
                        own.Sum(total => total.Count),
                        own.Where(total => total.IsPaid).Sum(total => total.Count),
                        own.Where(total => total.IsPaid).Sum(total => total.Amount),
                        own.Where(total => !total.IsPaid).Sum(total => total.Amount),
                        cancelledInPeriod.Count(lesson => lesson.TeacherId == teacher.Id),
                        cancelledInPeriod.Count(lesson => lesson.TeacherId == teacher.Id && lesson.IsLate));
                })
                .ToList();

            return new PeriodStatsDto(
                period.Period,
                period.From,
                period.To,
                inPeriod.Sum(total => total.Count),
                inPeriod.Where(total => total.IsPaid).Sum(total => total.Count),
                inPeriod.Where(total => total.IsPaid).Sum(total => total.Amount),
                inPeriod.Where(total => !total.IsPaid).Sum(total => total.Amount),
                byTeacher,
                cancelledInPeriod.Count,
                cancelledInPeriod.Count(lesson => lesson.IsLate),
                cancelledInPeriod.Count(lesson => lesson.ByAdmin),
                cancelledInPeriod
                    .GroupBy(lesson => (lesson.StudentId, lesson.StudentName))
                    .Select(group => new StudentCancellationsDto(
                        group.Key.StudentId, group.Key.StudentName, group.Count(), group.Count(lesson => lesson.IsLate)))
                    .OrderByDescending(student => student.CancelledCount)
                    .ThenByDescending(student => student.LateCount)
                    .ThenBy(student => student.Name)
                    .Take(MaxTopCancellers)
                    .ToList());
        }).ToList();

        return new LessonStatsDto(day, result);
    }

    private static List<(StatsPeriod Period, DateOnly From, DateOnly To)> GetPeriods(DateOnly day)
    {
        var weekStart = SchoolClock.StartOfWeek(day);
        var monthStart = new DateOnly(day.Year, day.Month, 1);
        var quarterStart = new DateOnly(day.Year, (day.Month - 1) / 3 * 3 + 1, 1);
        var yearStart = new DateOnly(day.Year, 1, 1);

        return
        [
            (StatsPeriod.Day, day, day),
            (StatsPeriod.Week, weekStart, weekStart.AddDays(6)),
            (StatsPeriod.Month, monthStart, monthStart.AddMonths(1).AddDays(-1)),
            (StatsPeriod.Quarter, quarterStart, quarterStart.AddMonths(3).AddDays(-1)),
            (StatsPeriod.Year, yearStart, yearStart.AddYears(1).AddDays(-1))
        ];
    }

    private const int MaxTopCancellers = 10;

    private sealed record Cancelled(DateOnly Date, Guid TeacherId, Guid? StudentId, string StudentName, bool IsLate, bool ByAdmin);

    private sealed record Total(DateOnly Date, Guid TeacherId, bool IsPaid, int Count, decimal Amount);
}
