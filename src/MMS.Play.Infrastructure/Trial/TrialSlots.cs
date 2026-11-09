using Microsoft.EntityFrameworkCore;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Contracts.Models.Trial;
using MMS.Play.Infrastructure.Persistence;

namespace MMS.Play.Infrastructure.Trial;

/// <summary>
/// Free times for a trial lesson: a room open at that hour (its <c>RoomHours</c>) and free, and a teacher without a
/// lesson then (lessons last <see cref="LessonDto.DurationMinutes"/> minutes, see LessonClashes). Requests still
/// waiting for an admin hold their time, so two newcomers are not promised the last free room.
/// </summary>
public static class TrialSlots
{
    private static readonly TimeSpan LessonDuration = TimeSpan.FromMinutes(LessonDto.DurationMinutes);

    /// <param name="context">Database.</param>
    /// <param name="earliest">School time; nothing earlier is offered.</param>
    /// <param name="lastDate">The last day offered.</param>
    /// <param name="cancellationToken">Aborts the queries.</param>
    public static async Task<IReadOnlyList<TrialDayDto>> FindAsync(
        ApplicationDbContext context,
        DateTime earliest,
        DateOnly lastDate,
        CancellationToken cancellationToken)
    {
        var firstDate = DateOnly.FromDateTime(earliest);
        if (firstDate > lastDate)
            return [];

        var rooms = await context.Rooms
            .AsNoTracking()
            .Include(room => room.Hours)
            .ToListAsync(cancellationToken);

        var teacherIds = await context.Teachers.AsNoTracking().Select(teacher => teacher.Id).ToListAsync(cancellationToken);
        if (rooms.Count == 0 || teacherIds.Count == 0)
            return [];

        var busy = await context.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.Date >= firstDate && lesson.Date <= lastDate && lesson.Status == LessonStatus.Scheduled)
            .Select(lesson => new { lesson.Date, lesson.StartTime, lesson.RoomId, lesson.TeacherId })
            .ToListAsync(cancellationToken);

        var held = await context.TrialRequests
            .AsNoTracking()
            .Where(request => request.Status == TrialRequestStatus.New
                              && request.PreferredDate >= firstDate
                              && request.PreferredDate <= lastDate
                              && request.PreferredTime != null)
            .Select(request => new { Date = request.PreferredDate!.Value, Time = request.PreferredTime!.Value })
            .ToListAsync(cancellationToken);

        var days = new List<TrialDayDto>();
        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            var day = date;
            var dayBusy = busy.Where(lesson => lesson.Date == day).ToList();
            var openRooms = rooms
                .Select(room => (room.Id, Hours: room.Hours.FirstOrDefault(hours => hours.DayOfWeek == day.DayOfWeek)))
                .Where(entry => entry.Hours is not null)
                .ToList();

            var times = openRooms
                .SelectMany(entry => HourRange(entry.Hours!.FirstLessonAt, entry.Hours.LastLessonAt))
                .Distinct()
                .Order()
                .Where(time => day.ToDateTime(time) >= earliest)
                .Where(time =>
                {
                    var freeRooms = openRooms.Count(entry =>
                        time >= entry.Hours!.FirstLessonAt
                        && time <= entry.Hours.LastLessonAt
                        && !dayBusy.Any(lesson => lesson.RoomId == entry.Id && Overlaps(lesson.StartTime, time)));
                    var freeTeachers = teacherIds.Count(teacherId =>
                        !dayBusy.Any(lesson => lesson.TeacherId == teacherId && Overlaps(lesson.StartTime, time)));
                    var waiting = held.Count(request => request.Date == day && Overlaps(request.Time, time));

                    return Math.Min(freeRooms, freeTeachers) - waiting > 0;
                })
                .ToList();

            if (times.Count > 0)
                days.Add(new TrialDayDto(day, times));
        }

        return days;
    }

    /// <summary>
    /// Whether the time is among the free ones.
    /// </summary>
    public static bool Contains(IReadOnlyList<TrialDayDto> days, DateOnly date, TimeOnly time) =>
        days.Any(day => day.Date == date && day.Times.Contains(time));

    // ticks, not TimeOnly subtraction: that wraps around midnight
    private static bool Overlaps(TimeOnly start, TimeOnly time) =>
        Math.Abs(start.Ticks - time.Ticks) < LessonDuration.Ticks;

    /// <summary>
    /// One start an hour from <paramref name="first"/> to <paramref name="last"/>, within the allowed lesson starts.
    /// </summary>
    private static IEnumerable<TimeOnly> HourRange(TimeOnly first, TimeOnly last)
    {
        for (var time = first; time <= last && time >= first; time = time.AddHours(1))
        {
            if (time >= LessonDto.EarliestStart && time <= LessonDto.LatestStart)
                yield return time;

            // AddHours wraps after 23:00; stop instead of looping forever
            if (time.Hour == 23)
                yield break;
        }
    }
}
