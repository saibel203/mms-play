namespace MMS.Play.Contracts.Models.Scores;

/// <param name="Content">Open file stream, disposed by the caller.</param>
/// <param name="FileName">Original file name, used for Content-Disposition.</param>
/// <param name="ContentType">MIME type derived from the format.</param>
/// <param name="Sha256">Hex SHA-256 of the content, a stable entity tag.</param>
/// <param name="UploadedAt">Upload time (UTC), used as Last-Modified.</param>
public record ScoreContent(Stream Content, string FileName, string ContentType, string Sha256, DateTime UploadedAt);
