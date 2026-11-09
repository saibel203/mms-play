using System.ComponentModel.DataAnnotations;

namespace MMS.Play.Presentation.Models.Requests.Telegram;

/// <summary>
/// The reminder checkbox of one subscriber.
/// </summary>
public class SetRemindersEnabledRequest
{
    /// <summary>
    /// true: the subscriber gets lesson reminders, false: not.
    /// </summary>
    // nullable so a missing field is a 400 instead of silently turning reminders off
    [Required]
    public bool? Enabled { get; set; }
}
