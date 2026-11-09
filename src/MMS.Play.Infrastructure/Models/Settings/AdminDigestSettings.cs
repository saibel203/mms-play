namespace MMS.Play.Infrastructure.Models.Settings;

/// <summary>
/// Bound from the "AdminDigest" section in appsettings: the daily summary admins get in the bot.
/// <see cref="Enabled"/> and <see cref="Time"/> are defaults: once an admin saves the notification settings,
/// the saved values are used.
/// </summary>
public class AdminDigestSettings
{
    public const string SectionName = "AdminDigest";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Time of day the summary goes out, school time.
    /// </summary>
    public TimeOnly Time { get; set; } = new(8, 0);
}
