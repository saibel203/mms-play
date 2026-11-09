using System.ComponentModel.DataAnnotations;

namespace MMS.Play.Presentation.Models.Requests.Trial;

public class DeclineTrialRequestRequest
{
    /// <summary>
    /// Sent to a bot requester with the answer; optional.
    /// </summary>
    [MaxLength(500)]
    public string? Reason { get; set; }
}
