using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;
using MMS.Play.Contracts.Models.Common;
using MMS.Play.Contracts.Models.Notifications;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;
using TelegramUser = Telegram.Bot.Types.User;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// The <see cref="TelegramSubscriber"/> table: the bot subscribes and unsubscribes users, an admin lists them and switches reminders.
/// </summary>
public class TelegramSubscriberService(ApplicationDbContext context) : ITelegramSubscriberService
{
    private static readonly Expression<Func<TelegramSubscriber, TelegramSubscriberDto>> ToDto = subscriber => new TelegramSubscriberDto(
        subscriber.Id,
        subscriber.TelegramUserId,
        subscriber.Username,
        subscriber.FirstName,
        subscriber.LastName,
        subscriber.IsActive,
        subscriber.RemindersEnabled,
        subscriber.SubscribedAtUtc,
        subscriber.UnsubscribedAtUtc,
        subscriber.LastNotifiedAtUtc,
        subscriber.IsAdmin,
        subscriber.IsParent,
        subscriber.Children
            .OrderBy(child => child.FirstName)
            .Select(child => new LinkedSubscriberDto(child.Id, child.LastName == null ? child.FirstName : child.FirstName + " " + child.LastName))
            .ToList());

    private const int MaxChildren = 10;

    public async Task<PagedResult<TelegramSubscriberDto>> ListAsync(TelegramSubscriberListQuery query, CancellationToken cancellationToken = default)
    {
        var subscribers = context.TelegramSubscribers.AsNoTracking();

        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim().TrimStart('@');
        if (search is not null)
        {
            subscribers = subscribers.Where(subscriber =>
                subscriber.FirstName.Contains(search)
                || (subscriber.LastName != null && subscriber.LastName.Contains(search))
                || (subscriber.Username != null && subscriber.Username.Contains(search)));
        }

        if (query.IsSubscribed is { } isSubscribed)
            subscribers = subscribers.Where(subscriber => subscriber.IsActive == isSubscribed);

        var totalCount = await subscribers.CountAsync(cancellationToken);

        // nothing to fetch past the last page, the long product also guards the int Skip below from overflowing
        IReadOnlyList<TelegramSubscriberDto> items = (query.Page - 1L) * query.PageSize >= totalCount
            ? []
            : await subscribers
                .OrderByDescending(subscriber => subscriber.SubscribedAtUtc)
                .ThenByDescending(subscriber => subscriber.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(ToDto)
                .ToListAsync(cancellationToken);

        return new PagedResult<TelegramSubscriberDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<TelegramSubscriberDto?> SetRemindersEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        var updated = await context.TelegramSubscribers
            .Where(subscriber => subscriber.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(subscriber => subscriber.RemindersEnabled, enabled), cancellationToken);

        if (updated == 0)
            return null;

        return await context.TelegramSubscribers
            .AsNoTracking()
            .Where(subscriber => subscriber.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TelegramSubscriberDto?> SetAdminAsync(Guid id, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var updated = await context.TelegramSubscribers
            .Where(subscriber => subscriber.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(subscriber => subscriber.IsAdmin, isAdmin), cancellationToken);

        if (updated == 0)
            return null;

        return await context.TelegramSubscribers
            .AsNoTracking()
            .Where(subscriber => subscriber.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TelegramSubscriberDto?> SetParentAsync(Guid id, bool isParent, IReadOnlyList<Guid> childIds, CancellationToken cancellationToken = default)
    {
        var parent = await context.TelegramSubscribers
            .Include(subscriber => subscriber.Children)
            .FirstOrDefaultAsync(subscriber => subscriber.Id == id, cancellationToken);

        if (parent is null)
            return null;

        var ids = childIds.Distinct().ToList();
        if (ids.Count > MaxChildren)
            throw new ValidationFailedException("ChildIds", $"A parent can have at most {MaxChildren} children.");
        if (ids.Contains(id))
            throw new ValidationFailedException("ChildIds", "A subscriber cannot be their own child.");

        var children = await context.TelegramSubscribers
            .Where(subscriber => ids.Contains(subscriber.Id))
            .ToListAsync(cancellationToken);

        if (children.Count != ids.Count)
            throw new ValidationFailedException("ChildIds", "Some of the children do not exist.");
        if (children.FirstOrDefault(child => child.IsParent) is { } childParent)
            throw new ValidationFailedException("ChildIds", $"{childParent.FirstName} is a parent, not a student.");
        // a student with linked parents would be both
        if (isParent && await context.TelegramSubscribers.AnyAsync(subscriber => subscriber.Children.Any(child => child.Id == id), cancellationToken))
            throw new ValidationFailedException("IsParent", $"{parent.FirstName} is linked as somebody's child, unlink first.");

        parent.IsParent = isParent;
        parent.Children.RemoveAll(child => !ids.Contains(child.Id));
        parent.Children.AddRange(children.Where(child => parent.Children.All(linked => linked.Id != child.Id)));
        await context.SaveChangesAsync(cancellationToken);

        return await context.TelegramSubscribers
            .AsNoTracking()
            .Where(subscriber => subscriber.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Called on /start. Creates the subscriber or reactivates it, refreshing the profile data that may have changed in Telegram.
    /// <see cref="TelegramSubscriber.RemindersEnabled"/> set by an admin is kept.
    /// </summary>
    /// <returns>True for a new subscriber, false when it already existed.</returns>
    public async Task<bool> SubscribeAsync(TelegramUser user, long chatId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var subscriber = await context.TelegramSubscribers
            .SingleOrDefaultAsync(x => x.TelegramUserId == user.Id, cancellationToken);

        var isNew = subscriber is null;
        if (subscriber is null)
        {
            subscriber = new TelegramSubscriber
            {
                Id = Guid.CreateVersion7(),
                TelegramUserId = user.Id,
                FirstName = user.FirstName,
                SubscribedAtUtc = now
            };
            context.TelegramSubscribers.Add(subscriber);
        }
        else if (!subscriber.IsActive)
        {
            subscriber.SubscribedAtUtc = now;
        }

        subscriber.ChatId = chatId;
        subscriber.FirstName = user.FirstName;
        subscriber.LastName = user.LastName;
        subscriber.Username = user.Username;
        subscriber.LanguageCode = user.LanguageCode;
        subscriber.IsActive = true;
        subscriber.UnsubscribedAtUtc = null;

        // updates are handled one at a time, two /start from the same user cannot race on the unique index
        await context.SaveChangesAsync(cancellationToken);
        return isNew;
    }

    /// <summary>
    /// Called on /stop and when the user blocks the bot.
    /// </summary>
    /// <returns>True when an active subscriber was deactivated.</returns>
    public async Task<bool> UnsubscribeAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var updated = await context.TelegramSubscribers
            .Where(x => x.TelegramUserId == telegramUserId && x.IsActive)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsActive, false)
                .SetProperty(x => x.UnsubscribedAtUtc, now), cancellationToken);

        return updated > 0;
    }
}
