using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.Models.Lessons;

namespace MMS.Play.Presentation.Models.Requests.Lessons;

/// <summary>
/// Why a lesson is cancelled.
/// </summary>
public class CancelLessonRequest
{
    /// <summary>
    /// Shown to the student and the parents in the bot; optional.
    /// </summary>
    [StringLength(LessonCancellationDto.ReasonMaxLength)]
    public string? Reason { get; set; }
}
