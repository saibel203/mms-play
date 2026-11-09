using System.ComponentModel.DataAnnotations;

namespace MMS.Play.Presentation.Models.Requests.Lessons;

/// <summary>
/// The "paid" checkbox of a lesson.
/// </summary>
public class SetLessonPaidRequest
{
    // nullable so a missing field is a 400 instead of silently marking the lesson unpaid
    [Required]
    public bool? IsPaid { get; set; }
}
