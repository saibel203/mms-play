using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Calendar;
using MMS.Play.Presentation.Controllers.Base;

namespace MMS.Play.Presentation.Controllers;

/// <summary>
/// Personal lesson calendars (iCalendar) that Google, Apple and other calendar apps subscribe to. Public: calendar
/// apps cannot sign in, the secret token in the link (given by the bot's «📆 Календар») is the key.
/// </summary>
[ApiVersion(1)]
[AllowAnonymous]
public class CalendarController(ICalendarFeedService calendarService) : BaseController
{
    private const string CalendarContentType = "text/calendar; charset=utf-8";

    /// <summary>
    /// The lessons of the token's owner and, for a parent, their children: 30 days back, 180 ahead; cancelled ones
    /// marked as cancelled, rescheduled ones left out.
    /// </summary>
    /// <param name="token">The personal link token.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The calendar.</response>
    /// <response code="404">Unknown or replaced token, or the subscriber left the bot.</response>
    [HttpGet("{token}.ics")]
    [Produces("text/calendar")]
    [ProducesResponseType<string>(StatusCodes.Status200OK, "text/calendar")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFeed(string token, CancellationToken cancellationToken)
    {
        var feed = await calendarService.GetFeedAsync(token, cancellationToken);
        if (feed is null)
            return NotFound();

        // calendar apps poll; a stale copy in a proxy would hide changes
        Response.Headers.CacheControl = "no-cache";
        return Content(feed, CalendarContentType);
    }
}
