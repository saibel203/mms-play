using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.Models.Notifications;

namespace MMS.Play.Infrastructure.Models.Settings;

/// <summary>
/// Bound from the "LessonReminders" section in appsettings. How often the job checks lives in "Jobs:LessonReminders".
/// <see cref="Enabled"/>, <see cref="DaysBefore"/> and <see cref="Times"/> are defaults: once an admin saves the
/// lesson settings, the saved values are used.
/// </summary>
public class LessonReminderSettings
{
    public const string SectionName = "LessonReminders";

    /// <summary>
    /// Default text, used until an admin saves a template. Telegram HTML (b, i, u, s, a, code tags) with the placeholders
    /// from <see cref="MessagePlaceholders.For"/>. A literal less-than, greater-than or ampersand
    /// must be written as an HTML entity, otherwise Telegram rejects the message.
    /// </summary>
    [Required]
    [MaxLength(MessageTemplateDto.TextMaxLength)]
    public string MessageTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Default text of what parents get about their children's lessons, same schedule as the students, used until an
    /// admin saves a text. Telegram HTML with the same placeholders, {FirstName} being the parent, plus {ChildName}.
    /// </summary>
    [Required]
    [MaxLength(MessageTemplateDto.TextMaxLength)]
    public string ParentMessageTemplate { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Reminders start this many days before a lesson (0 = on the day only) and go on until the lesson starts.
    /// </summary>
    [Range(0, NotificationSettingsDto.MaxReminderDaysBefore)]
    public int DaysBefore { get; set; } = 2;

    /// <summary>
    /// Times of day the reminders go out, school time: "09:00", "13:00".
    /// </summary>
    // no default values here: the configuration binder appends to a pre-filled list instead of replacing it
    [MinLength(1)]
    [MaxLength(NotificationSettingsDto.MaxReminderTimes)]
    public List<TimeOnly> Times { get; set; } = [];

    /// <summary>
    /// When set, reminders go out every this many minutes instead of at <see cref="Times"/>. For testing;
    /// "Jobs:LessonReminders:Schedule" has to run at least as often.
    /// </summary>
    [Range(1, NotificationSettingsDto.MaxReminderIntervalMinutes)]
    public int? IntervalMinutes { get; set; }

    /// <summary>
    /// Culture of {LessonDate}: "середа, 12.11" for uk-UA.
    /// </summary>
    [Required]
    public string Culture { get; set; } = "uk-UA";

    /// <summary>
    /// A reminder time is still served this long after it passed, e.g. after a restart.
    /// Older ones are skipped instead of being sent hours late.
    /// </summary>
    [Range(5, 24 * 60)]
    public int MissedSlotGraceMinutes { get; set; } = 120;

    /// <summary>
    /// Telegram allows about 30 messages per second in total, staying below it avoids 429 responses.
    /// </summary>
    [Range(1, 30)]
    public int MessagesPerSecond { get; set; } = 25;
}
