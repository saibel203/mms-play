using Microsoft.EntityFrameworkCore;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Calendar;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Infrastructure.Lessons;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Telegram;

namespace MMS.Play.Infrastructure.Calendar;

/// <summary>
/// The personal lesson calendar (iCalendar) behind the bot's «📆 Календар» link. Calendar apps poll it, so it is
/// built fresh on every request: a moved lesson moves (same UID), a cancelled one shows as cancelled, a rescheduled
/// one disappears and its replacement appears.
/// </summary>
public class CalendarFeedService(ApplicationDbContext context, SchoolClock clock) : ICalendarFeedService
{
    private const int DaysBack = 30;
    private const int DaysAhead = 180;

    // calendar apps decide on their own, but these ask for an hourly refresh
    private const string RefreshInterval = "PT1H";

    public async Task<string?> GetFeedAsync(string token, CancellationToken cancellationToken = default)
    {
        var subscriber = await TelegramSubscriberAccessToken.FindAsync(context, token, cancellationToken);
        if (subscriber is null)
            return null;

        // a parent sees their children's lessons, each with the child's name
        var childIds = subscriber.IsParent
            ? await context.TelegramSubscribers
                .Where(x => x.Id == subscriber.Id)
                .SelectMany(x => x.Children.Select(child => child.Id))
                .ToListAsync(cancellationToken)
            : [];
        var studentIds = childIds.Append(subscriber.Id).ToList();

        var today = clock.Today;
        var from = today.AddDays(-DaysBack);
        var to = today.AddDays(DaysAhead);

        var lessons = await context.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.StudentId != null
                             && studentIds.Contains(lesson.StudentId.Value)
                             && lesson.Date >= from
                             && lesson.Date <= to
                             && lesson.Status != LessonStatus.Rescheduled)
            .OrderBy(lesson => lesson.Date)
            .ThenBy(lesson => lesson.StartTime)
            .Select(lesson => new
            {
                lesson.Id,
                lesson.Date,
                lesson.StartTime,
                lesson.StudentId,
                StudentName = lesson.Student!.FirstName,
                Teacher = lesson.Teacher.Name,
                Room = lesson.Room.Name,
                lesson.Note,
                lesson.Status,
                lesson.CancellationReason,
                lesson.CreatedAtUtc,
                lesson.CancelledAtUtc
            })
            .ToListAsync(cancellationToken);

        var now = clock.UtcNow;
        var ics = new IcsWriter()
            .Begin("VCALENDAR")
            .Raw("VERSION", "2.0")
            .Raw("PRODID", "-//MMS Play//Lessons//UK")
            .Raw("CALSCALE", "GREGORIAN")
            .Raw("METHOD", "PUBLISH")
            .Text("X-WR-CALNAME", "Уроки музики")
            .Text("X-WR-CALDESC", $"Уроки {subscriber.FirstName}" + (childIds.Count > 0 ? " і дітей" : string.Empty))
            .Raw("X-WR-TIMEZONE", clock.TimeZone.Id)
            .Raw("REFRESH-INTERVAL;VALUE=DURATION", RefreshInterval)
            .Raw("X-PUBLISHED-TTL", RefreshInterval);

        foreach (var lesson in lessons)
        {
            // a time skipped by a daylight saving change has no UTC equivalent; such a lesson cannot take place
            if (clock.ToUtc(lesson.Date, lesson.StartTime) is not { } start)
                continue;

            var isCancelled = lesson.Status == LessonStatus.Cancelled;
            var isChild = lesson.StudentId != subscriber.Id;
            var summary = (isCancelled ? "❌ " : "🎵 ")
                          + (isChild ? $"{lesson.StudentName}: урок" : "Урок")
                          + $" — {lesson.Teacher}";

            var description = new List<string> { $"Викладач: {lesson.Teacher}", $"Кабінет: {lesson.Room}" };
            if (lesson.Note is { } note)
                description.Add(note);
            if (isCancelled)
                description.Add("Урок скасовано" + (lesson.CancellationReason is { } reason ? $": {reason}" : "."));

            ics.Begin("VEVENT")
                // stable: the same lesson stays the same event when it changes
                .Raw("UID", $"{lesson.Id:N}@mms-play")
                .Utc("DTSTAMP", now)
                .Utc("LAST-MODIFIED", lesson.CancelledAtUtc ?? lesson.CreatedAtUtc)
                .Utc("DTSTART", start)
                .Utc("DTEND", start.AddMinutes(LessonDto.DurationMinutes))
                .Text("SUMMARY", summary)
                .Text("LOCATION", $"Каб. {lesson.Room}")
                .Text("DESCRIPTION", string.Join("\n", description))
                .Raw("STATUS", isCancelled ? "CANCELLED" : "CONFIRMED")
                // a higher sequence tells the app the event changed
                .Raw("SEQUENCE", isCancelled ? "1" : "0")
                .Raw("TRANSP", "OPAQUE");

            if (!isCancelled)
            {
                // an hour before, phones remind on their own
                ics.Begin("VALARM")
                    .Raw("ACTION", "DISPLAY")
                    .Text("DESCRIPTION", summary)
                    .Raw("TRIGGER", "-PT1H")
                    .End("VALARM");
            }

            ics.End("VEVENT");
        }

        return ics.End("VCALENDAR").ToString();
    }
}
