using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Lessons;

namespace MMS.Play.Presentation.Models.Requests.Lessons;

/// <summary>
/// Marks several lessons as paid or unpaid.
/// </summary>
public class SetLessonsPaidRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(ILessonPaymentService.MaxBulkLessons)]
    public List<Guid> LessonIds { get; set; } = [];

    // nullable so a missing field is a 400 instead of silently marking the lessons unpaid
    [Required]
    public bool? IsPaid { get; set; }
}
