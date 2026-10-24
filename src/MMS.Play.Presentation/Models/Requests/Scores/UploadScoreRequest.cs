using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using MMS.Play.Contracts.Models.Scores;

namespace MMS.Play.Presentation.Models.Requests.Scores;

/// <summary>
/// Multipart form of a score upload.
/// </summary>
public class UploadScoreRequest
{
    /// <summary>
    /// Guitar Pro (.gp, .gp3, .gp4, .gp5, .gpx) or MusicXML (.musicxml, .xml) file.
    /// </summary>
    [Required]
    public IFormFile File { get; set; } = null!;

    /// <summary>
    /// Defaults to the file name without extension.
    /// </summary>
    [StringLength(ScoreUpload.TitleMaxLength)]
    public string? Title { get; set; }

    [StringLength(ScoreUpload.ArtistMaxLength)]
    public string? Artist { get; set; }
}
