namespace MMS.Play.Contracts.Models.Scores;

/// <summary>
/// Uploaded score file.
/// </summary>
/// <param name="Id">Score identifier.</param>
/// <param name="Title">Title given on upload, the file name without extension by default.</param>
/// <param name="Artist">Artist given on upload.</param>
/// <param name="FileName">Original file name.</param>
/// <param name="Format">File extension without the dot: gp, gp3, gp4, gp5, gpx, musicxml or xml.</param>
/// <param name="Size">File size in bytes.</param>
/// <param name="UploadedAt">Upload time (UTC).</param>
public record ScoreDto(
    Guid Id,
    string Title,
    string? Artist,
    string FileName,
    string Format,
    long Size,
    DateTime UploadedAt);
