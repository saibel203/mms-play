using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.Models.Lessons;

namespace MMS.Play.Presentation.Models.Requests.Lessons;

public class UpdateLessonSettingsRequest
{
    /// <summary>
    /// Price given to new lessons; existing lessons keep theirs.
    /// </summary>
    [Required]
    [Range(typeof(decimal), "0", "1000000")]
    public decimal? DefaultLessonPrice { get; set; }

    public LessonSettingsUpdate ToUpdate() => new(DefaultLessonPrice!.Value);
}
