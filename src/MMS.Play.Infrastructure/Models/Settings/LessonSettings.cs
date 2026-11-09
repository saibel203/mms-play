using System.ComponentModel.DataAnnotations;

namespace MMS.Play.Infrastructure.Models.Settings;

/// <summary>
/// Bound from the "Lessons" section in appsettings.
/// </summary>
public class LessonSettings
{
    public const string SectionName = "Lessons";

    /// <summary>
    /// The school's time zone (IANA or Windows id). Lesson dates and times, "today" and reminder times are in it.
    /// </summary>
    [Required]
    public string TimeZoneId { get; set; } = "Europe/Kyiv";

    /// <summary>
    /// Price of a new lesson until an admin saves another one.
    /// </summary>
    [Range(typeof(decimal), "0", "1000000")]
    public decimal DefaultLessonPrice { get; set; } = 500;

    /// <summary>
    /// A student moving a lesson in the bot picks from the free times of this many days ahead.
    /// </summary>
    [Range(1, 60)]
    public int RescheduleDaysAhead { get; set; } = 14;

    /// <summary>
    /// The earliest time offered for moving a lesson is this many hours from now.
    /// </summary>
    [Range(0, 72)]
    public int RescheduleMinNoticeHours { get; set; } = 2;

    /// <summary>
    /// Fills an empty timetable with demo students (Telegram subscribers that get no reminders) and lessons
    /// from three months back to two weeks ahead. Runs only while there are no lessons. For development.
    /// </summary>
    public bool SeedDemoData { get; set; }
}
