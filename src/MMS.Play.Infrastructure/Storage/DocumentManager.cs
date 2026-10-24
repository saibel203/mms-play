using System.Buffers;
using FluentStorage.Model;
using FluentStorage.Storage;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Storage;
using MMS.Play.Contracts.Models.Storage;

namespace MMS.Play.Infrastructure.Storage;

/// <summary>
/// <see cref="IDocumentManager"/> over a FluentStorage <see cref="IStore"/>, so the provider is chosen by configuration only.
/// </summary>
public class DocumentManager(IStore store) : IDocumentManager
{
    private const char Separator = '/';

    // ':' is valid in Linux file names but turns "C:" into a rooted path on Windows
    private static readonly SearchValues<char> InvalidSegmentChars = SearchValues.Create([.. Path.GetInvalidFileNameChars(), ':']);

    public Task SaveAsync(string path, Stream content, string? contentType = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        return contentType is null
            ? store.SetObject(NormalizePath(path), content, cancellationToken: cancellationToken)
            : store.SetObject(NormalizePath(path), content, contentType, cancellationToken: cancellationToken);
    }

    // FluentStorage returns null for a missing object instead of throwing
    public async Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default) =>
        await store.OpenRead(NormalizePath(path), cancellationToken);

    public async Task<DocumentInfo?> GetInfoAsync(string path, CancellationToken cancellationToken = default)
    {
        var storeObject = await store.GetObjectInfo(NormalizePath(path), cancellationToken);
        return storeObject is { IsFile: true } ? ToDocumentInfo(storeObject) : null;
    }

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default) =>
        store.ObjectExists(NormalizePath(path), cancellationToken);

    public async Task<IReadOnlyList<DocumentInfo>> ListAsync(string? folder = null, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var folderPath = string.IsNullOrWhiteSpace(folder) ? null : NormalizePath(folder);
        var storeObjects = await store.ListDirectory(folderPath, recursive, cancellationToken);

        return storeObjects
            .Where(storeObject => storeObject.IsFile)
            .Select(ToDocumentInfo)
            .ToList();
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default) =>
        store.DeleteObject(NormalizePath(path), cancellationToken);

    private static DocumentInfo ToDocumentInfo(StoreObject storeObject) =>
        new(storeObject.FullPath.TrimStart(Separator), storeObject.Size, storeObject.DateModified);

    /// <summary>
    /// The disk provider combines the path with its root as is, so "../x" or "C:/x" would escape the storage folder.
    /// </summary>
    private static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var segments = path.Replace('\\', Separator).Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0 || segments.Any(IsInvalidSegment))
            throw new ArgumentException($"'{path}' is not a valid document path.", nameof(path));

        return string.Join(Separator, segments);
    }

    private static bool IsInvalidSegment(string segment) =>
        segment is "." or ".." || segment.AsSpan().ContainsAny(InvalidSegmentChars);
}
