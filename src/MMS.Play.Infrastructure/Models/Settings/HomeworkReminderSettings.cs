using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.Models.Notifications;

namespace MMS.Play.Infrastructure.Models.Settings;

/// <summary>
/// Bound from the "HomeworkReminders" section in appsettings: the daily «time to practise» message and the message
/// about new homework. <see cref="Enabled"/> and <see cref="Time"/> are defaults: once an admin saves the notification
/// settings, the saved values are used. The texts are defaults until an admin saves their own.
/// </summary>
public class HomeworkReminderSettings
{
    public const string SectionName = "HomeworkReminders";

    /// <summary>
    /// When homework is given. Telegram HTML with <see cref="MessagePlaceholders.For"/> of <see cref="MessageTemplateKind.HomeworkAssigned"/>.
    /// </summary>
    [Required]
    [MaxLength(MessageTemplateDto.TextMaxLength)]
    public string AssignedMessageTemplate { get; set; } = string.Empty;

    /// <summary>
    /// The daily reminder. Placeholders of <see cref="MessageTemplateKind.HomeworkReminder"/>.
    /// </summary>
    [Required]
    [MaxLength(MessageTemplateDto.TextMaxLength)]
    public string ReminderMessageTemplate { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Time of day the reminders go out, school time.
    /// </summary>
    public TimeOnly Time { get; set; } = new(18, 0);

    /// <summary>
    /// The most homework listed in one message and given a «Позаймався» button; the rest is on the practice page.
    /// </summary>
    [Range(1, 20)]
    public int MaxListed { get; set; } = 5;
}
