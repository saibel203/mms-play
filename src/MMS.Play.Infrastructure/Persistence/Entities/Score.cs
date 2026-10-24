namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// Metadata of an uploaded score file, the file itself lives in the document storage under <see cref="StoragePath"/>.
/// </summary>
public class Score
{
    /// <summary>
    /// Version 7 GUID: time ordered, so inserts do not fragment the clustered index.
    /// </summary>
    public Guid Id { get; set; }

    public required string Title { get; set; }
    public string? Artist { get; set; }

    /// <summary>
    /// Original name of the uploaded file.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// Lower case extension without the dot, see ScoreFormat.
    /// </summary>
    public required string Extension { get; set; }

    public long Size { get; set; }

    /// <summary>
    /// Hex SHA-256 of the content, unique: the same file is stored once.
    /// </summary>
    public required string Sha256 { get; set; }

    /// <summary>
    /// Path relative to the document storage root.
    /// </summary>
    public required string StoragePath { get; set; }

    public DateTime UploadedAtUtc { get; set; }
}
