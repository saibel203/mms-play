namespace MMS.Play.Contracts.Models.Lessons;

/// <summary>
/// Lesson settings an admin changes from the UI. Until saved once, the defaults from appsettings are used.
/// What the bot sends and when is in the notification settings (Telegram page).
/// </summary>
/// <param name="DefaultLessonPrice">Price given to new lessons.</param>
/// <param name="TimeZone">The school's time zone (from appsettings, read only).</param>
/// <param name="IsDefault">True while nothing was saved: the values come from appsettings.</param>
/// <param name="UpdatedAt">When an admin last saved them (UTC).</param>
public record LessonSettingsDto(
    decimal DefaultLessonPrice,
    string TimeZone,
    bool IsDefault,
    DateTime? UpdatedAt);

/// <summary>
/// New values of the editable <see cref="LessonSettingsDto"/> fields.
/// </summary>
public record LessonSettingsUpdate(decimal DefaultLessonPrice);
