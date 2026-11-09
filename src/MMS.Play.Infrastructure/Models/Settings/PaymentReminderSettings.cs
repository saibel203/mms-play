using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.Models.Notifications;

namespace MMS.Play.Infrastructure.Models.Settings;

/// <summary>
/// Bound from the "PaymentReminders" section in appsettings: reminders about lessons that took place and are not paid.
/// <see cref="Enabled"/>, <see cref="Time"/> and <see cref="EveryDays"/> are defaults: once an admin saves the
/// lesson settings, the saved values are used. The texts are defaults until an admin saves their own.
/// </summary>
public class PaymentReminderSettings
{
    public const string SectionName = "PaymentReminders";

    /// <summary>
    /// To a student. Telegram HTML with <see cref="MessagePlaceholders.For"/> of <see cref="MessageTemplateKind.PaymentReminder"/>.
    /// </summary>
    [Required]
    [MaxLength(MessageTemplateDto.TextMaxLength)]
    public string StudentMessageTemplate { get; set; } = string.Empty;

    /// <summary>
    /// To a parent, one message per child that owes. Placeholders of <see cref="MessageTemplateKind.ParentPaymentReminder"/>.
    /// </summary>
    [Required]
    [MaxLength(MessageTemplateDto.TextMaxLength)]
    public string ParentMessageTemplate { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Time of day the reminders go out, school time.
    /// </summary>
    public TimeOnly Time { get; set; } = new(10, 0);

    /// <summary>
    /// The same person is reminded again after this many days while the lessons stay unpaid.
    /// </summary>
    [Range(1, 30)]
    public int EveryDays { get; set; } = 3;

    /// <summary>
    /// The longest list of lessons in {UnpaidLessons}; the rest is counted in one line.
    /// </summary>
    [Range(1, 50)]
    public int MaxListedLessons { get; set; } = 10;
}
