using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.Models.Lessons;

namespace MMS.Play.Presentation.Models.Requests.Lessons;

/// <summary>
/// A new or changed teacher.
/// </summary>
public class TeacherRequest
{
    /// <summary>
    /// First name as the timetable shows it, unique.
    /// </summary>
    [Required]
    [StringLength(TeacherSummaryDto.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The color of the teacher's lessons, "#rrggbb".
    /// </summary>
    [Required]
    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "The color must look like \"#22c55e\".")]
    public string Color { get; set; } = string.Empty;

    public TeacherInput ToInput() => new(Name, Color);
}
