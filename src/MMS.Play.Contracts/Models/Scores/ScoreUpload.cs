namespace MMS.Play.Contracts.Models.Scores;

/// <param name="Content">File content, must be seekable: it is read once for validation and hashing and once for storing.</param>
/// <param name="FileName">Original file name, the extension selects the format.</param>
/// <param name="Length">File size in bytes.</param>
/// <param name="Title">Defaults to the file name without extension.</param>
/// <param name="Artist">Optional.</param>
public record ScoreUpload(Stream Content, string FileName, long Length, string? Title = null, string? Artist = null)
{
    /// <summary>
    /// Key of <see cref="Exceptions.ValidationFailedException.Errors"/> for problems with the file itself.
    /// </summary>
    public const string FileField = "File";

    public const int FileNameMaxLength = 260;
    public const int TitleMaxLength = 256;
    public const int ArtistMaxLength = 256;
}
