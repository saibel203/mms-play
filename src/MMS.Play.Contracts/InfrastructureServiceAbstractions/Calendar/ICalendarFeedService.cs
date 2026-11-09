namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Calendar;

/// <summary>
/// The personal calendar feed (iCalendar) a student or parent subscribes to in Google or Apple Calendar.
/// </summary>
public interface ICalendarFeedService
{
    /// <summary>
    /// The .ics text with the lessons of the subscriber with this personal link token and, for a parent, their
    /// children; null for an unknown or replaced token.
    /// </summary>
    Task<string?> GetFeedAsync(string token, CancellationToken cancellationToken = default);
}
