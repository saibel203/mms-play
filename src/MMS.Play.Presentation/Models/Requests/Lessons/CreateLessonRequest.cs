using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.Models.Lessons;

namespace MMS.Play.Presentation.Models.Requests.Lessons;

public class CreateLessonRequest : LessonRequest
{
    /// <summary>
    /// The default lesson price from the settings when left out.
    /// </summary>
    [Range(typeof(decimal), "0", "1000000")]
    public decimal? Price { get; set; }

    /// <summary>
    /// 1 creates one lesson, more creates the same lesson in the following weeks too (at most 26).
    /// </summary>
    [Range(1, LessonDto.MaxRepeatWeeks)]
    public int RepeatWeeks { get; set; } = 1;

    public LessonInput ToInput(LessonSettingsDto settings) => ToInput(Price ?? settings.DefaultLessonPrice);
}
