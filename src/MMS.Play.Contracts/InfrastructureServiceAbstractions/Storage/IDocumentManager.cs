using MMS.Play.Contracts.Models.Storage;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Storage;

/// <summary>
/// Provider-agnostic access to stored files. Paths are relative to the storage root and use '/' as separator,
/// e.g. "scores/0192f4c1.gp5". Absolute paths and "." / ".." segments are rejected with <see cref="ArgumentException"/>.
/// </summary>
public interface IDocumentManager
{
    /// <summary>
    /// Writes the stream to the given path, an existing document is overwritten. Missing folders are created.
    /// </summary>
    Task SaveAsync(string path, Stream content, string? contentType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the document for reading, null when it does not exist. The caller owns and disposes the stream.
    /// </summary>
    Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Size and modification date of the document, null when it does not exist.
    /// </summary>
    Task<DocumentInfo?> GetInfoAsync(string path, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Documents in the folder (the storage root when null), folders themselves are not returned.
    /// </summary>
    Task<IReadOnlyList<DocumentInfo>> ListAsync(string? folder = null, bool recursive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the document, does nothing when it does not exist.
    /// </summary>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
}
