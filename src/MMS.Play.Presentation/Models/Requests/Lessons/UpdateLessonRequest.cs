using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.Models.Lessons;

namespace MMS.Play.Presentation.Models.Requests.Lessons;

public class UpdateLessonRequest : LessonRequest
{
    [Required]
    [Range(typeof(decimal), "0", "1000000")]
    public decimal? Price { get; set; }

    /// <summary>
    /// "Following" changes the later lessons of the series too (moved by the same number of days, same time, room,
    /// teacher, student, note and price; their payment stays). "This" by default.
    /// </summary>
    public LessonChangeScope Scope { get; set; } = LessonChangeScope.This;

    public LessonInput ToInput() => ToInput(Price!.Value);
}
