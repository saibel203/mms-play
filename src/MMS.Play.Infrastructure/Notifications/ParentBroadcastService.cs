using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;
using MMS.Play.Contracts.Models.Common;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Contracts.Models.Notifications;
using MMS.Play.Infrastructure.Lessons;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;
using MMS.Play.Infrastructure.Telegram;

namespace MMS.Play.Infrastructure.Notifications;

/// <summary>
/// Broadcasts for parents: who gets them, sending them and the answers. A broadcast is stored before it is sent,
/// every message gets a <see cref="ParentBroadcastDelivery"/> row whose id goes into the answer buttons.
/// </summary>
public partial class ParentBroadcastService(
    ApplicationDbContext context,
    IParentBroadcastSender sender,
    SchoolClock clock,
    IOptions<LessonReminderSettings> options,
    ILogger<ParentBroadcastService> logger) : IParentBroadcastService
{
    private const string NoValue = "—";

    private readonly LessonReminderSettings _settings = options.Value;
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo(options.Value.Culture);

    public async Task<IReadOnlyList<BroadcastRecipientDto>> ListRecipientsAsync(
        BroadcastAudience audience,
        DateOnly? lessonDate,
        CancellationToken cancellationToken = default)
    {
        var recipients = await QueryRecipientsAsync(audience, lessonDate, cancellationToken);
        return recipients.Select(ToDto).ToList();
    }

    public async Task<BroadcastDetailsDto> SendAsync(BroadcastInput input, string? createdBy, CancellationToken cancellationToken = default)
    {
        var (text, broadcastOptions) = Validate(input);

        if (!sender.IsAvailable)
            throw new ValidationFailedException("Telegram", "The Telegram bot is turned off (Telegram:Enabled), nothing can be sent.");

        // the list is read again: the one the admin saw may be stale, a removed child or lesson must not get a message
        var current = (await QueryRecipientsAsync(input.Audience, input.LessonDate, cancellationToken))
            .ToDictionary(recipient => (recipient.ParentId, recipient.Lesson?.LessonId));

        var targets = input.Recipients.Distinct().ToList();
        if (targets.Any(target => !current.ContainsKey((target.ParentId, target.LessonId))))
        {
            throw new ValidationFailedException(
                nameof(BroadcastInput.Recipients),
                "Some recipients are no longer on the list (a lesson or a child changed). Reload the list and send again.");
        }

        var now = clock.UtcNow;
        var today = clock.Today;
        var broadcast = new ParentBroadcast
        {
            Id = Guid.CreateVersion7(),
            Audience = input.Audience,
            LessonDate = input.Audience == BroadcastAudience.LessonDay ? input.LessonDate : null,
            Text = text,
            Options = broadcastOptions,
            AllowTextAnswer = input.AllowTextAnswer,
            CreatedBy = createdBy,
            CreatedAtUtc = now
        };

        foreach (var recipient in targets.Select(target => current[(target.ParentId, target.LessonId)]))
        {
            broadcast.Deliveries.Add(new ParentBroadcastDelivery
            {
                Id = Guid.CreateVersion7(),
                ParentId = recipient.ParentId,
                ChatId = recipient.ChatId,
                Text = Render(text, recipient, today),
                ChildName = recipient.ChildName,
                LessonId = recipient.Lesson?.LessonId,
                LessonDate = recipient.Lesson?.Date,
                LessonTime = recipient.Lesson?.StartTime,
                Status = recipient.IsActive ? BroadcastDeliveryStatus.Pending : BroadcastDeliveryStatus.Failed,
                Error = recipient.IsActive ? null : "Unsubscribed in the bot."
            });
        }

        context.ParentBroadcasts.Add(broadcast);
        await context.SaveChangesAsync(cancellationToken);

        // stored first, so a broadcast cut short (shutdown) still shows who got it.
        // Not cancelled with the request: closing the page must not stop a broadcast halfway
        await SendStoredAsync(broadcast, CancellationToken.None);

        return (await GetAsync(broadcast.Id, CancellationToken.None))!;
    }

    public async Task<PagedResult<BroadcastSummaryDto>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var totalCount = await context.ParentBroadcasts.CountAsync(cancellationToken);

        IReadOnlyList<BroadcastSummaryDto> items = (page - 1L) * pageSize >= totalCount
            ? []
            : await context.ParentBroadcasts
                .AsNoTracking()
                .OrderByDescending(broadcast => broadcast.CreatedAtUtc)
                .ThenByDescending(broadcast => broadcast.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(broadcast => new BroadcastSummaryDto(
                    broadcast.Id,
                    broadcast.CreatedAtUtc,
                    broadcast.CreatedBy,
                    broadcast.Audience,
                    broadcast.LessonDate,
                    broadcast.Text,
                    broadcast.Options.Count > 0 || broadcast.AllowTextAnswer,
                    broadcast.Deliveries.Count,
                    broadcast.Deliveries.Count(delivery => delivery.Status == BroadcastDeliveryStatus.Sent),
                    broadcast.Deliveries.Count(delivery => delivery.Status == BroadcastDeliveryStatus.Failed),
                    broadcast.Deliveries.Count(delivery => delivery.Answer != null)))
                .ToListAsync(cancellationToken);

        return new PagedResult<BroadcastSummaryDto>(items, page, pageSize, totalCount);
    }

    public async Task<BroadcastDetailsDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var broadcast = await context.ParentBroadcasts
            .AsNoTracking()
            .FirstOrDefaultAsync(broadcast => broadcast.Id == id, cancellationToken);

        if (broadcast is null)
            return null;

        var deliveries = await context.ParentBroadcastDeliveries
            .AsNoTracking()
            .Where(delivery => delivery.BroadcastId == id)
            .OrderBy(delivery => delivery.LessonDate)
            .ThenBy(delivery => delivery.LessonTime)
            .ThenBy(delivery => delivery.Parent.FirstName)
            .Select(delivery => new BroadcastDeliveryDto(
                delivery.Id,
                delivery.ParentId,
                delivery.Parent.LastName == null ? delivery.Parent.FirstName : delivery.Parent.FirstName + " " + delivery.Parent.LastName,
                delivery.Parent.Username,
                delivery.ChildName,
                delivery.LessonDate,
                delivery.LessonTime,
                delivery.Status,
                delivery.Error,
                delivery.SentAtUtc,
                delivery.Answer,
                delivery.AnswerOption,
                delivery.AnsweredAtUtc))
            .ToListAsync(cancellationToken);

        return new BroadcastDetailsDto(
            broadcast.Id,
            broadcast.CreatedAtUtc,
            broadcast.CreatedBy,
            broadcast.Audience,
            broadcast.LessonDate,
            broadcast.Text,
            broadcast.Options,
            broadcast.AllowTextAnswer,
            deliveries);
    }

    private async Task SendStoredAsync(ParentBroadcast broadcast, CancellationToken cancellationToken)
    {
        var delayBetweenMessages = TimeSpan.FromSeconds(1.0 / _settings.MessagesPerSecond);
        int sent = 0, failed = 0;

        foreach (var delivery in broadcast.Deliveries.Where(delivery => delivery.Status == BroadcastDeliveryStatus.Pending))
        {
            var result = await sender.SendAsync(broadcast, delivery, cancellationToken);

            if (result.MessageId is { } messageId)
            {
                delivery.Status = BroadcastDeliveryStatus.Sent;
                delivery.MessageId = messageId;
                delivery.SentAtUtc = clock.UtcNow;
                sent++;
            }
            else
            {
                delivery.Status = BroadcastDeliveryStatus.Failed;
                delivery.Error = Truncate(result.Error, 512);
                failed++;
            }

            if (result.Unreachable)
            {
                await context.TelegramSubscribers
                    .Where(subscriber => subscriber.Id == delivery.ParentId && subscriber.IsActive)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(subscriber => subscriber.IsActive, false)
                        .SetProperty(subscriber => subscriber.UnsubscribedAtUtc, clock.UtcNow), cancellationToken);
            }

            // saved one by one: the history is right even if sending stops halfway
            await context.SaveChangesAsync(cancellationToken);
            await Task.Delay(delayBetweenMessages, cancellationToken);
        }

        LogBroadcastSent(logger, broadcast.Id, sent, failed);
    }

    private (string Text, List<string> Options) Validate(BroadcastInput input)
    {
        var errors = new Dictionary<string, string[]>();

        // browsers send \r\n from a textarea, Telegram needs \n only
        var text = (input.Text ?? string.Empty).Replace("\r\n", "\n").Trim();
        if (text.Length == 0)
            errors[nameof(BroadcastInput.Text)] = ["Write the message."];
        else if (text.Length > ParentBroadcastLimits.TextMaxLength)
            errors[nameof(BroadcastInput.Text)] = [$"The message can be at most {ParentBroadcastLimits.TextMaxLength} characters long."];
        else if (TelegramHtml.Validate(text) is { } markupError)
            errors[nameof(BroadcastInput.Text)] = [markupError];
        else if (input.Audience == BroadcastAudience.AllParents
                 && ParentBroadcastLimits.LessonPlaceholders.FirstOrDefault(placeholder => text.Contains(placeholder, StringComparison.Ordinal)) is { } lessonPlaceholder)
            errors[nameof(BroadcastInput.Text)] = [$"{lessonPlaceholder} works only for the parents of the students with lessons on a day."];

        if (input.Audience == BroadcastAudience.LessonDay && input.LessonDate is null)
            errors[nameof(BroadcastInput.LessonDate)] = ["Pick the lesson day."];

        var options = (input.Options ?? [])
            .Select(option => (option ?? string.Empty).Trim())
            .Where(option => option.Length > 0)
            .ToList();
        if (options.Count > ParentBroadcastLimits.MaxOptions)
            errors[nameof(BroadcastInput.Options)] = [$"At most {ParentBroadcastLimits.MaxOptions} answers."];
        else if (options.Any(option => option.Length > ParentBroadcastLimits.OptionMaxLength))
            errors[nameof(BroadcastInput.Options)] = [$"An answer can be at most {ParentBroadcastLimits.OptionMaxLength} characters long."];
        else if (options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Count)
            errors[nameof(BroadcastInput.Options)] = ["The answers must differ."];

        var recipients = input.Recipients ?? [];
        if (recipients.Count == 0)
            errors[nameof(BroadcastInput.Recipients)] = ["Pick at least one recipient."];
        else if (recipients.Count > ParentBroadcastLimits.MaxRecipients)
            errors[nameof(BroadcastInput.Recipients)] = [$"At most {ParentBroadcastLimits.MaxRecipients} recipients at once."];

        if (errors.Count > 0)
            throw new ValidationFailedException(errors);

        return (text, options);
    }

    private async Task<List<Recipient>> QueryRecipientsAsync(BroadcastAudience audience, DateOnly? lessonDate, CancellationToken cancellationToken)
    {
        if (audience == BroadcastAudience.AllParents)
        {
            var parents = await context.TelegramSubscribers
                .AsNoTracking()
                .Where(subscriber => subscriber.IsParent)
                .OrderBy(subscriber => subscriber.FirstName)
                .ThenBy(subscriber => subscriber.LastName)
                .Select(parent => new
                {
                    parent.Id,
                    parent.FirstName,
                    parent.LastName,
                    parent.Username,
                    parent.ChatId,
                    parent.IsActive,
                    Children = parent.Children.OrderBy(child => child.FirstName).Select(child => child.FirstName).ToList()
                })
                .ToListAsync(cancellationToken);

            return parents
                .Select(parent => new Recipient(
                    parent.Id,
                    parent.FirstName,
                    parent.LastName,
                    parent.Username,
                    parent.ChatId,
                    parent.IsActive,
                    parent.Children.Count == 0 ? null : string.Join(", ", parent.Children),
                    null))
                .ToList();
        }

        if (lessonDate is not { } date)
            throw new ValidationFailedException("LessonDate", "Pick the lesson day.");

        // one row per lesson and parent of its student: two parents of one child both get a message
        var rows = await context.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.Date == date && lesson.Status == LessonStatus.Scheduled && lesson.Student != null)
            .SelectMany(lesson => lesson.Student!.Parents
                .Where(parent => parent.IsParent)
                .Select(parent => new
                {
                    parent.Id,
                    parent.FirstName,
                    parent.LastName,
                    parent.Username,
                    parent.ChatId,
                    parent.IsActive,
                    ChildName = lesson.Student!.FirstName,
                    LessonId = lesson.Id,
                    lesson.Date,
                    lesson.StartTime,
                    Room = lesson.Room.Name,
                    Teacher = lesson.Teacher.Name
                }))
            .OrderBy(row => row.StartTime)
            .ThenBy(row => row.FirstName)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new Recipient(
                row.Id,
                row.FirstName,
                row.LastName,
                row.Username,
                row.ChatId,
                row.IsActive,
                row.ChildName,
                new BroadcastLessonDto(row.LessonId, row.Date, row.StartTime, row.Room, row.Teacher)))
            .ToList();
    }

    private string Render(string text, Recipient recipient, DateOnly today)
    {
        // the text is HTML, a name like "<Max>" would break the markup
        var rendered = text
            .Replace(ParentBroadcastLimits.FirstNamePlaceholder, WebUtility.HtmlEncode(recipient.FirstName), StringComparison.Ordinal)
            .Replace(ParentBroadcastLimits.ChildNamePlaceholder, WebUtility.HtmlEncode(recipient.ChildName ?? NoValue), StringComparison.Ordinal);

        if (recipient.Lesson is not { } lesson)
            return rendered;

        return rendered
            .Replace(MessagePlaceholders.LessonDay, UkrainianDates.When(lesson.Date, today), StringComparison.Ordinal)
            .Replace(MessagePlaceholders.LessonDate, lesson.Date.ToString("dddd, dd.MM", _culture), StringComparison.Ordinal)
            .Replace(MessagePlaceholders.LessonTime, UkrainianDates.Time(lesson.StartTime), StringComparison.Ordinal)
            .Replace(MessagePlaceholders.Room, WebUtility.HtmlEncode(lesson.Room), StringComparison.Ordinal)
            .Replace(MessagePlaceholders.Teacher, WebUtility.HtmlEncode(lesson.Teacher), StringComparison.Ordinal);
    }

    private static BroadcastRecipientDto ToDto(Recipient recipient) => new(
        recipient.ParentId,
        recipient.LastName is null ? recipient.FirstName : $"{recipient.FirstName} {recipient.LastName}",
        recipient.Username,
        recipient.IsActive,
        recipient.ChildName,
        recipient.Lesson);

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];

    private sealed record Recipient(
        Guid ParentId,
        string FirstName,
        string? LastName,
        string? Username,
        long ChatId,
        bool IsActive,
        string? ChildName,
        BroadcastLessonDto? Lesson);

    [LoggerMessage(Level = LogLevel.Information, Message = "Broadcast {BroadcastId} sent: {Sent} sent, {Failed} failed")]
    private static partial void LogBroadcastSent(ILogger logger, Guid broadcastId, int sent, int failed);
}
