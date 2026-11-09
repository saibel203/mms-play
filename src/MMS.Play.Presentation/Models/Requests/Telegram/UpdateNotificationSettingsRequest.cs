using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.Models.Notifications;

namespace MMS.Play.Presentation.Models.Requests.Telegram;

/// <summary>
/// When and what the bot sends on its own. Every flag and time is required, so a missing field is a 400
/// instead of silently turning something off.
/// </summary>
public class UpdateNotificationSettingsRequest
{
    /// <summary>
    /// Remind students and parents of upcoming lessons.
    /// </summary>
    [Required]
    public bool? RemindersEnabled { get; set; }

    /// <summary>
    /// Lesson reminders start this many days before a lesson (0 = on the day only), at most 14.
    /// </summary>
    [Required]
    [Range(0, NotificationSettingsDto.MaxReminderDaysBefore)]
    public int? ReminderDaysBefore { get; set; }

    /// <summary>
    /// Times of day for lesson reminders, school time: ["09:00:00", "13:00:00"]. 1 to 6 different times.
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(NotificationSettingsDto.MaxReminderTimes)]
    public List<TimeOnly> ReminderTimes { get; set; } = [];

    /// <summary>
    /// When set, lesson reminders go out every this many minutes (1 to 1440) instead of at the times. For testing; empty for the times.
    /// </summary>
    [Range(1, NotificationSettingsDto.MaxReminderIntervalMinutes)]
    public int? ReminderIntervalMinutes { get; set; }

    /// <summary>
    /// Remind students and parents of lessons that took place and are not paid.
    /// </summary>
    [Required]
    public bool? PaymentRemindersEnabled { get; set; }

    /// <summary>
    /// When the payment reminders go out, school time: "10:00:00".
    /// </summary>
    [Required]
    public TimeOnly? PaymentReminderTime { get; set; }

    /// <summary>
    /// The same person is reminded again after this many days (1 to 30) while the lessons stay unpaid.
    /// </summary>
    [Required]
    [Range(1, NotificationSettingsDto.MaxPaymentReminderEveryDays)]
    public int? PaymentReminderEveryDays { get; set; }

    /// <summary>
    /// Where students and parents pay: an absolute http(s) URL, e.g. a Monobank jar. Empty for none.
    /// </summary>
    [MaxLength(NotificationSettingsDto.PaymentLinkMaxLength)]
    public string? PaymentLink { get; set; }

    /// <summary>
    /// Payment details as plain text: recipient, IBAN, purpose. Empty for none.
    /// </summary>
    [MaxLength(NotificationSettingsDto.PaymentDetailsMaxLength)]
    public string? PaymentDetails { get; set; }

    /// <summary>
    /// Send students with active homework the daily «time to practise» message.
    /// </summary>
    [Required]
    public bool? HomeworkRemindersEnabled { get; set; }

    /// <summary>
    /// When the homework reminders go out, school time: "18:00:00".
    /// </summary>
    [Required]
    public TimeOnly? HomeworkReminderTime { get; set; }

    /// <summary>
    /// Send admins the daily summary in the bot.
    /// </summary>
    [Required]
    public bool? AdminDigestEnabled { get; set; }

    /// <summary>
    /// When the daily summary goes out, school time: "08:00:00".
    /// </summary>
    [Required]
    public TimeOnly? AdminDigestTime { get; set; }

    /// <summary>
    /// Let newcomers ask for a trial lesson, in the bot and on the public page.
    /// </summary>
    [Required]
    public bool? TrialBookingEnabled { get; set; }

    public NotificationSettingsUpdate ToUpdate() =>
        new(
            RemindersEnabled!.Value,
            ReminderDaysBefore!.Value,
            ReminderTimes,
            ReminderIntervalMinutes,
            PaymentRemindersEnabled!.Value,
            PaymentReminderTime!.Value,
            PaymentReminderEveryDays!.Value,
            PaymentLink,
            PaymentDetails,
            HomeworkRemindersEnabled!.Value,
            HomeworkReminderTime!.Value,
            AdminDigestEnabled!.Value,
            AdminDigestTime!.Value,
            TrialBookingEnabled!.Value);
}
