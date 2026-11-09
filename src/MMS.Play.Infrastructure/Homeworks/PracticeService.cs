using Microsoft.EntityFrameworkCore;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Homework;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Scores;
using MMS.Play.Contracts.Models.Homework;
using MMS.Play.Contracts.Models.Scores;
using MMS.Play.Infrastructure.Lessons;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Telegram;

namespace MMS.Play.Infrastructure.Homeworks;

/// <summary>
/// The practice page behind a personal link: the link owner sees the active homework of their own and, as a parent,
/// of their children. Completed homework is not shown and its score cannot be downloaded through the link.
/// </summary>
public class PracticeService(ApplicationDbContext context, IScoreService scoreService, SchoolClock clock) : IPracticeService
{
    public async Task<PracticeOverviewDto?> GetOverviewAsync(string token, CancellationToken cancellationToken = default)
    {
        var owner = await FindOwnerAsync(token, cancellationToken);
        if (owner is null)
            return null;

        var rows = await HomeworkRow.ListAsync(
            ActiveOf(owner.StudentIds).OrderByDescending(x => x.CreatedAtUtc),
            cancellationToken);

        var today = clock.Today;
        return new PracticeOverviewDto(owner.FirstName, [.. rows.Select(row => row.ToPracticeDto(today))]);
    }

    public async Task<ScoreContent?> OpenScoreAsync(string token, Guid homeworkId, CancellationToken cancellationToken = default)
    {
        var owner = await FindOwnerAsync(token, cancellationToken);
        if (owner is null)
            return null;

        var scoreId = await ActiveOf(owner.StudentIds)
            .Where(x => x.Id == homeworkId)
            .Select(x => x.ScoreId)
            .FirstOrDefaultAsync(cancellationToken);

        return scoreId is { } id ? await scoreService.OpenContentAsync(id, cancellationToken) : null;
    }

    public async Task<PracticeHomeworkDto?> MarkPracticedAsync(string token, Guid homeworkId, CancellationToken cancellationToken = default)
    {
        var owner = await FindOwnerAsync(token, cancellationToken);
        if (owner is null)
            return null;

        if (!await ActiveOf(owner.StudentIds).AnyAsync(x => x.Id == homeworkId, cancellationToken))
            return null;

        var today = clock.Today;
        await HomeworkStats.MarkPracticedAsync(context, homeworkId, today, clock.UtcNow, cancellationToken);

        return (await HomeworkRow.FindAsync(context, homeworkId, cancellationToken))?.ToPracticeDto(today);
    }

    private IQueryable<Persistence.Entities.Homework> ActiveOf(List<Guid> studentIds) =>
        context.Homeworks.Where(x => studentIds.Contains(x.StudentId) && x.Status == HomeworkStatus.Active);

    /// <summary>
    /// The token's subscriber and whose homework they see: their own and their children's.
    /// </summary>
    private async Task<Owner?> FindOwnerAsync(string token, CancellationToken cancellationToken)
    {
        var subscriber = await TelegramSubscriberAccessToken.FindAsync(context, token, cancellationToken);
        if (subscriber is null)
            return null;

        var children = subscriber.IsParent
            ? await context.TelegramSubscribers
                .Where(x => x.Id == subscriber.Id)
                .SelectMany(x => x.Children.Select(child => child.Id))
                .ToListAsync(cancellationToken)
            : [];

        return new Owner(subscriber.FirstName, [subscriber.Id, .. children]);
    }

    private sealed record Owner(string FirstName, List<Guid> StudentIds);
}
