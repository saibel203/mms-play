using System.ComponentModel.DataAnnotations;

namespace MMS.Play.Jobs.Models.Settings;

/// <summary>
/// Bound from the "Jobs:LessonReminders" section in appsettings. Message template and send rate live in "LessonReminders".
/// </summary>
public class SendLessonRemindersJobSettings
{
    public const string SectionName = "Jobs:LessonReminders";

    /// <summary>
    /// Needs "Telegram:Enabled": the services that send messages are registered only with the bot.
    /// When false, the job is not registered and the recurring job is removed from Hangfire.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Cron expression, evaluated in UTC.
    /// </summary>
    [Required]
    public string Schedule { get; set; } = "* * * * *";
}
