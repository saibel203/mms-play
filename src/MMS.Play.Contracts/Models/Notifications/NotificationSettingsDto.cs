namespace MMS.Play.Contracts.Models.Notifications;

/// <summary>
/// Everything about the messages the bot sends on its own, edited on the Telegram page. Until saved once, the
/// defaults from appsettings are used.
/// </summary>
/// <param name="RemindersEnabled">Whether the bot sends lesson reminders at all.</param>
/// <param name="ReminderDaysBefore">Reminders start this many days before the lesson (0 = on the day only) and go on until it starts.</param>
/// <param name="ReminderTimes">Times of day the lesson reminders are sent, school time. Not used while <paramref name="ReminderIntervalMinutes"/> is set.</param>
/// <param name="ReminderIntervalMinutes">When set, lesson reminders go out every this many minutes instead of at <paramref name="ReminderTimes"/>. For testing.</param>
/// <param name="PaymentRemindersEnabled">Whether students and parents are reminded of lessons that took place and are not paid.</param>
/// <param name="PaymentReminderTime">When the payment reminders go out, school time.</param>
/// <param name="PaymentReminderEveryDays">The same person is reminded again after this many days while the lessons stay unpaid.</param>
/// <param name="PaymentLink">Where students and parents pay (http/https URL), the «Оплатити» button in the bot; null when not set.</param>
/// <param name="PaymentDetails">Payment details as plain text (recipient, IBAN, purpose), sent by the bot; null when not set.</param>
/// <param name="HomeworkRemindersEnabled">Whether students with active homework get the daily «time to practise» message.</param>
/// <param name="HomeworkReminderTime">When the homework reminders go out, school time.</param>
/// <param name="AdminDigestEnabled">Whether admins get the daily summary in the bot.</param>
/// <param name="AdminDigestTime">When the daily summary goes out, school time.</param>
/// <param name="TrialBookingEnabled">Whether newcomers may ask for a trial lesson, in the bot and on the public page.</param>
/// <param name="TimeZone">The school's time zone (from appsettings, read only).</param>
/// <param name="IsDefault">True while nothing was saved: the values come from appsettings.</param>
/// <param name="UpdatedAt">When an admin last saved them (UTC).</param>
public record NotificationSettingsDto(
    bool RemindersEnabled,
    int ReminderDaysBefore,
    IReadOnlyList<TimeOnly> ReminderTimes,
    int? ReminderIntervalMinutes,
    bool PaymentRemindersEnabled,
    TimeOnly PaymentReminderTime,
    int PaymentReminderEveryDays,
    string? PaymentLink,
    string? PaymentDetails,
    bool HomeworkRemindersEnabled,
    TimeOnly HomeworkReminderTime,
    bool AdminDigestEnabled,
    TimeOnly AdminDigestTime,
    bool TrialBookingEnabled,
    string TimeZone,
    bool IsDefault,
    DateTime? UpdatedAt)
{
    public const int MaxReminderDaysBefore = 14;
    public const int MaxReminderTimes = 6;
    public const int MaxReminderIntervalMinutes = 24 * 60;
    public const int MaxPaymentReminderEveryDays = 30;
    public const int PaymentLinkMaxLength = 500;
    public const int PaymentDetailsMaxLength = 1000;
}

/// <summary>
/// New values of the editable <see cref="NotificationSettingsDto"/> fields.
/// </summary>
public record NotificationSettingsUpdate(
    bool RemindersEnabled,
    int ReminderDaysBefore,
    IReadOnlyList<TimeOnly> ReminderTimes,
    int? ReminderIntervalMinutes,
    bool PaymentRemindersEnabled,
    TimeOnly PaymentReminderTime,
    int PaymentReminderEveryDays,
    string? PaymentLink,
    string? PaymentDetails,
    bool HomeworkRemindersEnabled,
    TimeOnly HomeworkReminderTime,
    bool AdminDigestEnabled,
    TimeOnly AdminDigestTime,
    bool TrialBookingEnabled);
