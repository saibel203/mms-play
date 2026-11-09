namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// Settings an admin saves from the UI, a single row with <see cref="Id"/> = <see cref="SingletonId"/>.
/// Until it exists the defaults from appsettings are used.
/// </summary>
public class SchoolSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public decimal DefaultLessonPrice { get; set; }

    public bool RemindersEnabled { get; set; }
    public int ReminderDaysBefore { get; set; }

    /// <summary>
    /// School time, sorted. Stored as a JSON array.
    /// </summary>
    public List<TimeOnly> ReminderTimes { get; set; } = [];

    /// <summary>
    /// When set, reminders go out every this many minutes instead of at <see cref="ReminderTimes"/>. For testing.
    /// </summary>
    public int? ReminderIntervalMinutes { get; set; }

    /// <summary>
    /// Whether students and parents are reminded of lessons that took place and are not paid.
    /// </summary>
    public bool PaymentRemindersEnabled { get; set; }

    /// <summary>
    /// When payment reminders go out, school time.
    /// </summary>
    public TimeOnly PaymentReminderTime { get; set; }

    /// <summary>
    /// The same person is reminded again after this many days while the lessons stay unpaid.
    /// </summary>
    public int PaymentReminderEveryDays { get; set; }

    /// <summary>
    /// Where students and parents pay, an absolute http(s) URL (a Monobank jar, a payment page). Shown as the
    /// «Оплатити» button in the bot and as {PaymentLink} in payment reminders.
    /// </summary>
    public string? PaymentLink { get; set; }

    /// <summary>
    /// Payment details as plain text: recipient, IBAN, purpose. Shown by /pay and as {PaymentDetails}.
    /// </summary>
    public string? PaymentDetails { get; set; }

    /// <summary>
    /// Whether students with active homework get the daily «time to practise» message.
    /// </summary>
    public bool HomeworkRemindersEnabled { get; set; }

    /// <summary>
    /// When the homework reminders go out, school time.
    /// </summary>
    public TimeOnly HomeworkReminderTime { get; set; }

    /// <summary>
    /// Whether admins get the daily summary in the bot.
    /// </summary>
    public bool AdminDigestEnabled { get; set; }

    /// <summary>
    /// When the daily summary goes out, school time.
    /// </summary>
    public TimeOnly AdminDigestTime { get; set; }

    /// <summary>
    /// Whether newcomers may ask for a trial lesson, in the bot and on the public page.
    /// </summary>
    public bool TrialBookingEnabled { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
