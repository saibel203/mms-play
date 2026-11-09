using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.Models.Lessons;

namespace MMS.Play.Presentation.Models.Requests.Lessons;

/// <summary>
/// A new or changed room.
/// </summary>
public class RoomRequest
{
    /// <summary>
    /// Room number or name, e.g. "314", unique.
    /// </summary>
    [Required]
    [StringLength(RoomSummaryDto.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Open days: { "dayOfWeek": "Monday", "firstLessonAt": "10:00:00", "lastLessonAt": "20:00:00" }, one slot an hour.
    /// A day left out is closed; lessons outside the hours are still allowed.
    /// </summary>
    [MaxLength(7)]
    public List<RoomHoursDto> Hours { get; set; } = [];

    public RoomInput ToInput() => new(Name, Hours);
}
