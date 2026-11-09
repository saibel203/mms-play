using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.Models.Trial;

namespace MMS.Play.Presentation.Models.Requests.Trial;

/// <summary>
/// A trial lesson request from the public page.
/// </summary>
public class CreateTrialRequestRequest
{
    /// <summary>
    /// Who comes to the lesson.
    /// </summary>
    [Required]
    [MaxLength(TrialRequestInput.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Phone or @telegram.
    /// </summary>
    [Required]
    [MaxLength(TrialRequestInput.ContactMaxLength)]
    public string Contact { get; set; } = string.Empty;

    /// <summary>
    /// Instrument, age, experience; optional.
    /// </summary>
    [MaxLength(TrialRequestInput.CommentMaxLength)]
    public string? Comment { get; set; }

    /// <summary>
    /// One of the free days from GET slots, "2026-11-12"; with <see cref="Time"/>, or both empty for "any time".
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// One of that day's free times, "15:00:00".
    /// </summary>
    public TimeOnly? Time { get; set; }

    /// <summary>
    /// Spam trap: hidden on the page, people leave it empty, bots fill it in. A filled one is accepted and dropped.
    /// </summary>
    public string? Website { get; set; }

    public TrialRequestInput ToInput() => new(Name, Contact, Comment, Date, Time);
}
