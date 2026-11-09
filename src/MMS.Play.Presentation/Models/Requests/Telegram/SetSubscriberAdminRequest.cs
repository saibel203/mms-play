using System.ComponentModel.DataAnnotations;

namespace MMS.Play.Presentation.Models.Requests.Telegram;

/// <summary>
/// The "admin" checkbox of one subscriber.
/// </summary>
public class SetSubscriberAdminRequest
{
    /// <summary>
    /// true: gets the students' reschedule requests in the bot and may answer them there.
    /// </summary>
    // nullable so a missing field is a 400 instead of silently taking the role away
    [Required]
    public bool? IsAdmin { get; set; }
}
