using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Logging;
using MMS.Play.Contracts.Models.Common;
using MMS.Play.Contracts.Models.Logging;
using MMS.Play.Infrastructure.Persistence;

namespace MMS.Play.Infrastructure.Logging;

public class LogQueryService(ApplicationDbContext context) : ILogQueryService
{
    // the MSSqlServer sink stores the whole event in LogEvent, the rest of the row already holds everything but its properties
    private const string PropertiesJsonProperty = "Properties";

    private static readonly LogSeverity[] Severities = Enum.GetValues<LogSeverity>();

    public async Task<PagedResult<LogEntryDto>> ListAsync(LogListQuery query, CancellationToken cancellationToken = default)
    {
        var logs = context.Logs.AsNoTracking();

        if (query.MinLevel is { } minLevel)
        {
            // levels are stored as names: "Warning and above" becomes Level IN ('Warning', 'Error', 'Fatal'), a seek per level on IX_Logs_Level_TimeStamp
            var levels = Severities.Where(level => level >= minLevel).Select(level => level.ToString()).ToList();
            logs = logs.Where(log => levels.Contains(log.Level!));
        }

        if (query.FromUtc is { } fromUtc)
            logs = logs.Where(log => log.TimeStamp >= fromUtc);

        if (query.ToUtc is { } toUtc)
            logs = logs.Where(log => log.TimeStamp < toUtc);

        if (!string.IsNullOrWhiteSpace(query.Source))
        {
            // LIKE 'prefix%' can use an index, a namespace selects all of its classes
            var source = query.Source.Trim();
            logs = logs.Where(log => log.SourceContext!.StartsWith(source));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // LIKE '%text%' scans the rows left by the other filters, combine it with a period or a level on large tables
            var search = query.Search.Trim();
            logs = logs.Where(log => log.Message!.Contains(search) || log.Exception!.Contains(search));
        }

        var totalCount = await logs.CountAsync(cancellationToken);

        // nothing to fetch past the last page, the long product also guards the int Skip below from overflowing
        IReadOnlyList<LogEntryDto> items = (query.Page - 1L) * query.PageSize >= totalCount
            ? []
            : await logs
                .OrderByDescending(log => log.TimeStamp)
                .ThenByDescending(log => log.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                // LogEvent and MessageTemplate are not selected: the event JSON is the largest column by far
                .Select(log => new LogEntryDto(log.Id, log.TimeStamp, log.Level, log.SourceContext, log.Message, log.Exception))
                .ToListAsync(cancellationToken);

        return new PagedResult<LogEntryDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<LogEntryDetailsDto?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var log = await context.Logs
            .AsNoTracking()
            .FirstOrDefaultAsync(log => log.Id == id, cancellationToken);

        if (log is null)
            return null;

        return new LogEntryDetailsDto(
            log.Id,
            log.TimeStamp,
            log.Level,
            log.SourceContext,
            log.Message,
            log.MessageTemplate,
            log.Exception,
            ReadProperties(log.LogEvent));
    }

    private static JsonElement? ReadProperties(string? logEvent)
    {
        if (string.IsNullOrWhiteSpace(logEvent))
            return null;

        try
        {
            using var document = JsonDocument.Parse(logEvent);

            // Clone: the element must outlive the disposed document
            return document.RootElement.TryGetProperty(PropertiesJsonProperty, out var properties)
                ? properties.Clone()
                : document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // a broken event must not hide the rest of the entry
            return null;
        }
    }
}
