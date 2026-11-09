using System.ComponentModel.DataAnnotations;

namespace MMS.Play.Presentation.Models.Requests.Lessons;

/// <summary>
/// Copies the lessons of the week before into a week.
/// </summary>
public class CopyPreviousWeekRequest
{
    /// <summary>
    /// Any day of the week to fill (Monday to Sunday), "2026-11-16".
    /// </summary>
    // nullable so a missing field is a 400 instead of silently copying into 0001-01-01
    [Required]
    public DateOnly? Date { get; set; }
}
