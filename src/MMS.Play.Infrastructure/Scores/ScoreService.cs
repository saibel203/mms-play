using System.Linq.Expressions;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Scores;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Storage;
using MMS.Play.Contracts.Models.Common;
using MMS.Play.Contracts.Models.Scores;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Scores;

public partial class ScoreService(
    ApplicationDbContext context,
    IDocumentManager documentManager,
    IOptions<ScoreSettings> options,
    ILogger<ScoreService> logger) : IScoreService
{
    // translated to SQL, only the listed columns are selected
    private static readonly Expression<Func<Score, ScoreDto>> ToDto = score => new ScoreDto(
        score.Id,
        score.Title,
        score.Artist,
        score.FileName,
        score.Extension,
        score.Size,
        score.UploadedAtUtc);

    private static readonly Func<Score, ScoreDto> MapToDto = ToDto.Compile();

    private readonly ScoreSettings _settings = options.Value;

    public async Task<ScoreUploadResult> UploadAsync(ScoreUpload upload, CancellationToken cancellationToken = default)
    {
        var format = await ValidateAsync(upload, cancellationToken);
        var fileName = Path.GetFileName(upload.FileName);
        var sha256 = await ComputeSha256Async(upload.Content, cancellationToken);

        var existing = await FindBySha256Async(sha256, cancellationToken);
        if (existing is not null)
        {
            LogDuplicateUpload(logger, upload.FileName, existing.Id);
            return new ScoreUploadResult(existing, IsDuplicate: true);
        }

        var id = Guid.CreateVersion7();
        var score = new Score
        {
            Id = id,
            Title = NormalizeText(upload.Title) ?? DefaultTitle(fileName),
            Artist = NormalizeText(upload.Artist),
            FileName = fileName,
            Extension = format.Extension,
            Size = upload.Length,
            Sha256 = sha256,
            StoragePath = $"{_settings.Folder}/{id:N}.{format.Extension}",
            UploadedAtUtc = DateTime.UtcNow
        };

        // the file goes first: a row without a file would be listed but could not be opened
        await documentManager.SaveAsync(score.StoragePath, upload.Content, format.ContentType, cancellationToken);

        try
        {
            context.Scores.Add(score);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // CancellationToken.None: the orphaned file must be removed even when the request was aborted
            await DeleteOrphanedFileAsync(score.StoragePath);

            // the same file uploaded concurrently, the unique index on Sha256 let only one insert through
            if (ex is DbUpdateException && await FindBySha256Async(sha256, CancellationToken.None) is { } concurrent)
                return new ScoreUploadResult(concurrent, IsDuplicate: true);

            throw;
        }

        LogScoreUploaded(logger, score.Id, score.FileName, score.Size);
        return new ScoreUploadResult(MapToDto(score), IsDuplicate: false);
    }

    public async Task<PagedResult<ScoreDto>> ListAsync(ScoreListQuery query, CancellationToken cancellationToken = default)
    {
        var scores = context.Scores.AsNoTracking();

        var search = NormalizeText(query.Search);
        if (search is not null)
        {
            scores = scores.Where(score =>
                score.Title.Contains(search)
                || (score.Artist != null && score.Artist.Contains(search))
                || score.FileName.Contains(search));
        }

        var totalCount = await scores.CountAsync(cancellationToken);

        // nothing to fetch past the last page, the long product also guards the int Skip below from overflowing
        IReadOnlyList<ScoreDto> items = (query.Page - 1L) * query.PageSize >= totalCount
            ? []
            : await scores
                .OrderByDescending(score => score.UploadedAtUtc)
                .ThenByDescending(score => score.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(ToDto)
                .ToListAsync(cancellationToken);

        return new PagedResult<ScoreDto>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<ScoreDto?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Scores
            .AsNoTracking()
            .Where(score => score.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ScoreContent?> OpenContentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var file = await context.Scores
            .AsNoTracking()
            .Where(score => score.Id == id)
            .Select(score => new { score.FileName, score.Extension, score.Sha256, score.StoragePath, score.UploadedAtUtc })
            .FirstOrDefaultAsync(cancellationToken);

        if (file is null)
            return null;

        var stream = await documentManager.OpenReadAsync(file.StoragePath, cancellationToken);
        if (stream is null)
        {
            LogFileMissing(logger, id, file.StoragePath);
            return null;
        }

        return new ScoreContent(stream, file.FileName, ScoreFormat.Get(file.Extension).ContentType, file.Sha256, file.UploadedAtUtc);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var storagePath = await context.Scores
            .Where(score => score.Id == id)
            .Select(score => score.StoragePath)
            .FirstOrDefaultAsync(cancellationToken);

        if (storagePath is null)
            return false;

        // the row goes first: a leftover file is invisible, a row without a file would be listed but could not be opened.
        // 0 rows means a concurrent request has already deleted it
        if (await context.Scores.Where(score => score.Id == id).ExecuteDeleteAsync(cancellationToken) == 0)
            return false;

        await DeleteOrphanedFileAsync(storagePath);

        LogScoreDeleted(logger, id);
        return true;
    }

    private async Task<ScoreFormat> ValidateAsync(ScoreUpload upload, CancellationToken cancellationToken)
    {
        if (!upload.Content.CanSeek)
            throw new ArgumentException("The upload stream must be seekable.", nameof(upload));

        if (!ScoreFormat.TryFind(Path.GetExtension(upload.FileName), out var format))
            throw Invalid($"Unsupported file type. Supported extensions: {string.Join(", ", ScoreFormat.SupportedExtensions)}.");

        if (Path.GetFileName(upload.FileName).Length > ScoreUpload.FileNameMaxLength)
            throw Invalid($"The file name exceeds {ScoreUpload.FileNameMaxLength} characters.");

        // the request model checks these too, the service must not rely on it
        if (NormalizeText(upload.Title)?.Length > ScoreUpload.TitleMaxLength)
            throw new ValidationFailedException(nameof(ScoreUpload.Title), $"The title exceeds {ScoreUpload.TitleMaxLength} characters.");

        if (NormalizeText(upload.Artist)?.Length > ScoreUpload.ArtistMaxLength)
            throw new ValidationFailedException(nameof(ScoreUpload.Artist), $"The artist exceeds {ScoreUpload.ArtistMaxLength} characters.");

        if (upload.Length == 0)
            throw Invalid("The file is empty.");

        if (upload.Length > _settings.MaxFileSizeBytes)
            throw Invalid($"The file exceeds the maximum size of {_settings.MaxFileSizeBytes / 1024 / 1024.0:0.#} MB.");

        if (!format.MatchesSignature(await ReadHeaderAsync(upload.Content, cancellationToken)))
            throw Invalid($"The file content is not a valid .{format.Extension} file.");

        return format;

        static ValidationFailedException Invalid(string error) => new(ScoreUpload.FileField, error);
    }

    private static async Task<byte[]> ReadHeaderAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[ScoreFormat.HeaderLength];

        stream.Position = 0;
        var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);

        return read == buffer.Length ? buffer : buffer[..read];
    }

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken)
    {
        stream.Position = 0;
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        stream.Position = 0;

        return Convert.ToHexStringLower(hash);
    }

    private Task<ScoreDto?> FindBySha256Async(string sha256, CancellationToken cancellationToken) =>
        context.Scores
            .AsNoTracking()
            .Where(score => score.Sha256 == sha256)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task DeleteOrphanedFileAsync(string storagePath)
    {
        try
        {
            await documentManager.DeleteAsync(storagePath, CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogOrphanedFileDeleteFailed(logger, ex, storagePath);
        }
    }

    // a 260 character file name leaves more than the title column holds
    private static string DefaultTitle(string fileName)
    {
        var title = Path.GetFileNameWithoutExtension(fileName);
        return title.Length > ScoreUpload.TitleMaxLength ? title[..ScoreUpload.TitleMaxLength] : title;
    }

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [LoggerMessage(Level = LogLevel.Information, Message = "Score {ScoreId} uploaded from {FileName} ({Size} bytes)")]
    private static partial void LogScoreUploaded(ILogger logger, Guid scoreId, string fileName, long size);

    [LoggerMessage(Level = LogLevel.Information, Message = "Upload of {FileName} skipped, the same file is already stored as score {ScoreId}")]
    private static partial void LogDuplicateUpload(ILogger logger, string fileName, Guid scoreId);

    [LoggerMessage(Level = LogLevel.Error, Message = "File of score {ScoreId} is missing in the document storage: {StoragePath}")]
    private static partial void LogFileMissing(ILogger logger, Guid scoreId, string storagePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not delete orphaned score file {StoragePath}")]
    private static partial void LogOrphanedFileDeleteFailed(ILogger logger, Exception exception, string storagePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Score {ScoreId} deleted")]
    private static partial void LogScoreDeleted(ILogger logger, Guid scoreId);
}
