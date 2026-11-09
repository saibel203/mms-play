using System.ComponentModel.DataAnnotations;

namespace MMS.Play.Infrastructure.Models.Settings;

/// <summary>
/// Bound from the "TrialBooking" section in appsettings: trial lessons newcomers ask for in the bot or on the public
/// page. <see cref="Enabled"/> is the default until an admin saves the notification settings.
/// </summary>
public class TrialBookingSettings
{
    public const string SectionName = "TrialBooking";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Free times are offered for this many days from today.
    /// </summary>
    [Range(1, 60)]
    public int DaysAhead { get; set; } = 14;

    /// <summary>
    /// The earliest time offered is this many hours from now, so the school has time to answer.
    /// </summary>
    [Range(0, 168)]
    public int MinNoticeHours { get; set; } = 24;

    /// <summary>
    /// Spam guard of the public page: more requests from it within an hour are refused.
    /// </summary>
    [Range(1, 1000)]
    public int MaxWebsiteRequestsPerHour { get; set; } = 10;
}
