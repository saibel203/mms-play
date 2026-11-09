using Microsoft.Extensions.Options;
using MMS.Play.Infrastructure.Models.Settings;

namespace MMS.Play.Infrastructure.Lessons;

/// <summary>
/// "Now" and "today" in the school's time zone ("Lessons:TimeZoneId"), whatever the server's own zone is.
/// </summary>
public class SchoolClock(IOptions<LessonSettings> options, TimeProvider timeProvider)
{
    public TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);

    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>
    /// School local time, <see cref="DateTimeKind.Unspecified"/>.
    /// </summary>
    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(UtcNow, TimeZone);

    public DateOnly Today => DateOnly.FromDateTime(Now);

    /// <summary>
    /// Null for a time that does not exist locally (skipped by a daylight saving change).
    /// </summary>
    public DateTime? ToUtc(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time);
        return TimeZone.IsInvalidTime(local) ? null : TimeZoneInfo.ConvertTimeToUtc(local, TimeZone);
    }

    /// <summary>
    /// Monday of the week containing <paramref name="day"/>.
    /// </summary>
    public static DateOnly StartOfWeek(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
}
