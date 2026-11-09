using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.Models.Trial;

namespace MMS.Play.Presentation.Models.Requests.Trial;

/// <summary>
/// Where and when the trial lesson takes place.
/// </summary>
public class BookTrialRequestRequest
{
    [Required]
    public Guid? TeacherId { get; set; }

    [Required]
    public Guid? RoomId { get; set; }

    /// <summary>
    /// "2026-11-12".
    /// </summary>
    [Required]
    public DateOnly? Date { get; set; }

    /// <summary>
    /// "15:00:00".
    /// </summary>
    [Required]
    public TimeOnly? StartTime { get; set; }

    /// <summary>
    /// Empty for the default lesson price; 0 for a free trial lesson.
    /// </summary>
    [Range(typeof(decimal), "0", "1000000")]
    public decimal? Price { get; set; }

    public TrialBookingInput ToInput() => new(TeacherId!.Value, RoomId!.Value, Date!.Value, StartTime!.Value, Price);
}
