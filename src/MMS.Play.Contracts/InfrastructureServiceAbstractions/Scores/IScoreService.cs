using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.Models.Common;
using MMS.Play.Contracts.Models.Scores;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Scores;

/// <summary>
/// Guitar Pro / MusicXML files: metadata is kept in the database, the file itself in <see cref="Storage.IDocumentManager"/>.
/// </summary>
public interface IScoreService
{
    /// <summary>
    /// Validates and stores the file. Uploading a file with the same content again returns the existing score
    /// with <see cref="ScoreUploadResult.IsDuplicate"/> set instead of storing a second copy.
    /// </summary>
    /// <exception cref="ValidationFailedException">The file is empty, too large, has an unsupported extension
    /// or its content does not match the extension.</exception>
    Task<ScoreUploadResult> UploadAsync(ScoreUpload upload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Newest scores first.
    /// </summary>
    Task<PagedResult<ScoreDto>> ListAsync(ScoreListQuery query, CancellationToken cancellationToken = default);

    Task<ScoreDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the stored file, null when the score or its file does not exist. The caller disposes <see cref="ScoreContent.Content"/>.
    /// </summary>
    Task<ScoreContent?> OpenContentAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the score and its file, false when the score does not exist.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
