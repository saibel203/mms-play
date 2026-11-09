namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// Message text edited by an admin. A missing row means the default from appsettings is used.
/// </summary>
public class MessageTemplate
{
    /// <summary>
    /// What the template is for, e.g. <c>LessonReminder</c>.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Telegram HTML with placeholders.
    /// </summary>
    public required string Text { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
