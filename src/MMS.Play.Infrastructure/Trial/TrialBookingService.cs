using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Lessons;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Trial;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Contracts.Models.Trial;
using MMS.Play.Infrastructure.Lessons;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Trial;

/// <summary>
/// Trial lessons: free times, requests from the public page and the bot, booking them as lessons
/// (through <see cref="ILessonService"/>, so the usual checks and clash rules apply).
/// </summary>
public partial class TrialBookingService(
    ApplicationDbContext context,
    SchoolClock clock,
    ILessonService lessonService,
    ILessonSettingsService lessonSettingsService,
    INotificationSettingsService notificationSettingsService,
    TrialEligibility eligibility,
    ITrialNotifier notifier,
    IOptions<TrialBookingSettings> options,
    ILogger<TrialBookingService> logger) : ITrialBookingService
{
    public const int DeclineReasonMaxLength = 500;

    private readonly TrialBookingSettings _settings = options.Value;

    private static readonly Expression<Func<TrialRequest, TrialRequestDto>> ToDto = request => new TrialRequestDto(
        request.Id,
        request.Source,
        request.SubscriberId,
        request.Name,
        request.Contact,
        request.Comment,
        request.PreferredDate,
        request.PreferredTime,
        request.Status,
        request.LessonId,
        request.Lesson != null ? (DateOnly?)request.Lesson.Date : null,
        request.Lesson != null ? (TimeOnly?)request.Lesson.StartTime : null,
        request.CreatedAtUtc,
        request.DecidedAtUtc);

    /// <summary>
    /// How a request from the bot went.
    /// </summary>
    public enum BotOutcome
    {
        Created,

        /// <summary>
        /// The subscriber may not ask (any more), see <see cref="TrialEligibility.Explain"/>.
        /// </summary>
        NotEligible,

        /// <summary>
        /// The time got taken meanwhile.
        /// </summary>
        SlotTaken
    }

    /// <summary>
    /// Free times are offered for this many days.
    /// </summary>
    public int DaysAhead => _settings.DaysAhead;

    public async Task<TrialSlotsDto> GetSlotsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await notificationSettingsService.GetAsync(cancellationToken);
        if (!settings.TrialBookingEnabled)
            return new TrialSlotsDto(false, []);

        return new TrialSlotsDto(true, await FindSlotsAsync(cancellationToken));
    }

    public async Task CreateFromWebsiteAsync(TrialRequestInput input, CancellationToken cancellationToken = default)
    {
        var settings = await notificationSettingsService.GetAsync(cancellationToken);
        if (!settings.TrialBookingEnabled)
            throw new ValidationFailedException("Request", "Booking a trial lesson online is closed at the moment, please call the school.");

        var name = input.Name?.Trim() ?? "";
        var contact = input.Contact?.Trim() ?? "";
        var comment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim();

        var errors = new Dictionary<string, string[]>();
        if (name.Length is < 2 or > TrialRequestInput.NameMaxLength)
            errors[nameof(TrialRequestInput.Name)] = [$"Give a name, 2 to {TrialRequestInput.NameMaxLength} characters."];
        if (contact.Length is < 3 or > TrialRequestInput.ContactMaxLength)
            errors[nameof(TrialRequestInput.Contact)] = ["Give a phone number or a Telegram @username so we can reach you."];
        if (comment?.Length > TrialRequestInput.CommentMaxLength)
            errors[nameof(TrialRequestInput.Comment)] = [$"The comment is longer than {TrialRequestInput.CommentMaxLength} characters."];
        if (input.Date.HasValue != input.Time.HasValue)
            errors[nameof(TrialRequestInput.Time)] = ["Pick both a day and a time, or neither."];
        if (errors.Count > 0)
            throw new ValidationFailedException(errors);

        // the spam guard: an hour's worth of requests from the page is plenty for a school
        var hourAgo = clock.UtcNow.AddHours(-1);
        var lastHour = await context.TrialRequests.CountAsync(
            request => request.Source == TrialRequestSource.Website && request.CreatedAtUtc >= hourAgo, cancellationToken);
        if (lastHour >= _settings.MaxWebsiteRequestsPerHour)
            throw new ValidationFailedException("Request", "Too many requests right now, please try again in an hour or call the school.");

        var waiting = await context.TrialRequests.AnyAsync(
            request => request.Status == TrialRequestStatus.New && request.Contact == contact, cancellationToken);
        if (waiting)
            throw new ValidationFailedException("Request", "We already have your request and will contact you soon.");

        if (input is { Date: { } date, Time: { } time }
            && !TrialSlots.Contains(await FindSlotsAsync(cancellationToken), date, time))
            throw new ValidationFailedException(nameof(TrialRequestInput.Time), "This time is no longer free, please pick another one.");

        var request = new TrialRequest
        {
            Id = Guid.CreateVersion7(),
            Source = TrialRequestSource.Website,
            Name = name,
            Contact = contact,
            Comment = comment,
            PreferredDate = input.Date,
            PreferredTime = input.Time,
            Status = TrialRequestStatus.New,
            CreatedAtUtc = clock.UtcNow
        };

        context.TrialRequests.Add(request);
        await context.SaveChangesAsync(cancellationToken);
        LogRequested(logger, request.Id, request.Source);

        await notifier.NotifyAdminsAsync(request, cancellationToken);
    }

    /// <summary>
    /// A request from the bot; name and contact come from Telegram. Null date and time = "any time".
    /// </summary>
    public async Task<(BotOutcome Outcome, TrialRequest? Request)> CreateFromBotAsync(
        long telegramUserId,
        DateOnly? date,
        TimeOnly? time,
        CancellationToken cancellationToken)
    {
        // checked again: the buttons may be old
        if (await eligibility.CheckAsync(telegramUserId, cancellationToken) != TrialEligibility.Result.Eligible)
            return (BotOutcome.NotEligible, null);

        if (date is { } day && time is { } start && !TrialSlots.Contains(await FindSlotsAsync(cancellationToken), day, start))
            return (BotOutcome.SlotTaken, null);

        var subscriber = await context.TelegramSubscribers.FirstAsync(x => x.TelegramUserId == telegramUserId, cancellationToken);
        var name = $"{subscriber.FirstName} {subscriber.LastName}".Trim();
        var request = new TrialRequest
        {
            Id = Guid.CreateVersion7(),
            Source = TrialRequestSource.Bot,
            SubscriberId = subscriber.Id,
            Subscriber = subscriber,
            Name = Cut(name, TrialRequestInput.NameMaxLength),
            Contact = subscriber.Username is null ? null : "@" + subscriber.Username,
            PreferredDate = date,
            PreferredTime = time,
            Status = TrialRequestStatus.New,
            CreatedAtUtc = clock.UtcNow
        };

        context.TrialRequests.Add(request);
        await context.SaveChangesAsync(cancellationToken);
        LogRequested(logger, request.Id, request.Source);

        await notifier.NotifyAdminsAsync(request, cancellationToken);
        return (BotOutcome.Created, request);
    }

    /// <summary>
    /// Free times for the bot, the same as for the public page.
    /// </summary>
    public Task<IReadOnlyList<TrialDayDto>> FindSlotsAsync(CancellationToken cancellationToken) =>
        TrialSlots.FindAsync(
            context,
            clock.Now.AddHours(_settings.MinNoticeHours),
            clock.Today.AddDays(_settings.DaysAhead),
            cancellationToken);

    public async Task<IReadOnlyList<TrialRequestDto>> ListAsync(TrialRequestStatus? status, CancellationToken cancellationToken = default)
    {
        var query = context.TrialRequests.AsNoTracking();
        if (status is { } wanted)
            query = query.Where(request => request.Status == wanted);

        return await query
            .OrderByDescending(request => request.CreatedAtUtc)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    public async Task<TrialRequestDto?> BookAsync(Guid id, TrialBookingInput input, CancellationToken cancellationToken = default)
    {
        var request = await context.TrialRequests
            .Include(x => x.Subscriber)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null)
            return null;
        EnsureNew(request);

        var price = input.Price ?? (await lessonSettingsService.GetAsync(cancellationToken)).DefaultLessonPrice;
        // a newcomer from the page is not in the bot: the note tells the admin who comes
        var note = request.SubscriberId is null
            ? $"Пробний: {request.Name}, {request.Contact}"
            : "Пробний урок";

        // validation and clash checks of a normal lesson
        var created = await lessonService.CreateAsync(
            new LessonInput(input.Date, input.StartTime, input.RoomId, input.TeacherId, request.SubscriberId,
                Cut(note, LessonDto.NoteMaxLength), price, IsPaid: false),
            repeatWeeks: 1,
            cancellationToken);
        var lessonId = created[0].Id;

        // two admins booking at once: only one wins, the other's lesson goes away again
        var now = clock.UtcNow;
        var updated = await context.TrialRequests
            .Where(x => x.Id == id && x.Status == TrialRequestStatus.New)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, TrialRequestStatus.Booked)
                .SetProperty(x => x.LessonId, lessonId)
                .SetProperty(x => x.DecidedAtUtc, now), cancellationToken);
        if (updated == 0)
        {
            await lessonService.DeleteAsync(lessonId, cancellationToken: cancellationToken);
            throw new ValidationFailedException("Status", "Somebody has just answered this request.");
        }

        LogBooked(logger, id, lessonId);

        var lesson = await context.Lessons
            .AsNoTracking()
            .Include(x => x.Room)
            .Include(x => x.Teacher)
            .FirstAsync(x => x.Id == lessonId, cancellationToken);
        await notifier.NotifyBookedAsync(request, lesson, cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task<TrialRequestDto?> DeclineAsync(Guid id, string? reason, CancellationToken cancellationToken = default)
    {
        var request = await context.TrialRequests
            .Include(x => x.Subscriber)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null)
            return null;
        EnsureNew(request);

        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (reason?.Length > DeclineReasonMaxLength)
            throw new ValidationFailedException("Reason", $"The reason is longer than {DeclineReasonMaxLength} characters.");

        request.Status = TrialRequestStatus.Declined;
        request.DecidedAtUtc = clock.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        LogDeclined(logger, id);

        await notifier.NotifyDeclinedAsync(request, reason, cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private Task<TrialRequestDto?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.TrialRequests.AsNoTracking().Where(x => x.Id == id).Select(ToDto).FirstOrDefaultAsync(cancellationToken);

    private static void EnsureNew(TrialRequest request)
    {
        if (request.Status != TrialRequestStatus.New)
            throw new ValidationFailedException("Status", request.Status == TrialRequestStatus.Booked
                ? "This request is booked already."
                : "This request was declined.");
    }

    private static string Cut(string text, int maxLength) => text.Length <= maxLength ? text : text[..maxLength];

    [LoggerMessage(Level = LogLevel.Information, Message = "Trial lesson request {RequestId} from the {Source}")]
    private static partial void LogRequested(ILogger logger, Guid requestId, TrialRequestSource source);

    [LoggerMessage(Level = LogLevel.Information, Message = "Trial lesson request {RequestId} booked as lesson {LessonId}")]
    private static partial void LogBooked(ILogger logger, Guid requestId, Guid lessonId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Trial lesson request {RequestId} declined")]
    private static partial void LogDeclined(ILogger logger, Guid requestId);
}
