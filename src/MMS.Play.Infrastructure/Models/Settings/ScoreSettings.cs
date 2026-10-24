using System.ComponentModel.DataAnnotations;

namespace MMS.Play.Infrastructure.Models.Settings;

/// <summary>
/// Bound from the "Scores" section in appsettings.
/// </summary>
public class ScoreSettings
{
    public const string SectionName = "Scores";

    /// <summary>
    /// Folder inside the document storage where score files are written.
    /// </summary>
    [Required]
    public string Folder { get; set; } = "scores";

    /// <summary>
    /// Larger uploads are rejected. Must stay below Kestrel's request body limit (30 MB by default).
    /// </summary>
    [Range(1, 30 * 1024 * 1024)]
    public int MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}
