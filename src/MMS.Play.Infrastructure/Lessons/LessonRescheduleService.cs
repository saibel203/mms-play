using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Lessons;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Lessons;

/// <summary>
/// Moving a lesson at the student's request: the student picks one of the teacher's free times in the bot,
/// an admin approves (the old lesson is kept as rescheduled, a new one takes the time) or declines
/// (the student picks again, the declined time crossed out).
/// </summary>
public partial class LessonRescheduleService(
    ApplicationDbContext context,
    SchoolClock clock,
    IRescheduleNotifier notifier,
    IOptions<LessonSettings> options,
    ILogger<LessonRescheduleService> logger) : ILessonRescheduleService
{
    private readonly LessonSettings _settings = options.Value;

    /// <summary>
    /// The free times a student can move the lesson to, or why the lesson cannot be moved.
    /// </summary>
    public async Task<(RescheduleOptions? Options, RescheduleProblem? Problem)> GetOptionsAsync(
        Guid lessonId,
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        var lesson = await FindStudentLessonAsync(lessonId, telegramUserId, cancellationToken);
        if (lesson is null)
            return (null, RescheduleProblem.NotFound);

        if (!IsMovable(lesson))
            return (null, RescheduleProblem.NotMovable);

        if (lesson.RescheduleRequests.Any(request => request.Status == RescheduleRequestStatus.Pending))
            return (null, RescheduleProblem.AlreadyPending);

        return (await BuildOptionsAsync(lesson, cancellationToken), null);
    }

    /// <summary>
    /// Records the time the student picked and sends it to the admins.
    /// </summary>
    public async Task<(RescheduleRequestInfo? Request, RescheduleProblem? Problem)> ProposeAsync(
        Guid lessonId,
        long telegramUserId,
        long chatId,
        DateOnly date,
        TimeOnly time,
        CancellationToken cancellationToken)
    {
        var (options, problem) = await GetOptionsAsync(lessonId, telegramUserId, cancellationToken);
        if (options is null)
            return (null, problem);

        // checked again here: the buttons the student pressed may be minutes old
        var slot = options.Slots.FirstOrDefault(slot => slot.Date == date && slot.Time == time);
        if (slot is null || options.Declined.Contains((date, time)))
            return (null, RescheduleProblem.SlotTaken);

        var request = new LessonRescheduleRequest
        {
            Id = Guid.CreateVersion7(),
            LessonId = lessonId,
            ProposedDate = date,
            ProposedTime = time,
            ProposedRoomId = slot.RoomId,
            Status = RescheduleRequestStatus.Pending,
            StudentChatId = chatId,
            CreatedAtUtc = clock.UtcNow
        };

        context.LessonRescheduleRequests.Add(request);
        await context.SaveChangesAsync(cancellationToken);

        var info = new RescheduleRequestInfo(request.Id, options.Lesson, date, time, slot.RoomName, chatId);
        request.AdminMessages = await notifier.NotifyAdminsAsync(info, cancellationToken);
        await context.SaveChangesAsync(CancellationToken.None);

        LogProposed(logger, lessonId, date, time, request.AdminMessages.Count);
        return (info, null);
    }

    /// <summary>
    /// The student withdraws a pending proposal.
    /// </summary>
    /// <returns>False when it is not pending any more (answered meanwhile) or not this student's.</returns>
    public async Task<bool> WithdrawAsync(Guid requestId, long telegramUserId, CancellationToken cancellationToken)
    {
        var request = await LoadRequestAsync(requestId, cancellationToken);
        if (request?.Lesson.Student is not { } student || student.TelegramUserId != telegramUserId)
            return false;

        if (!await TryCloseAsync(requestId, RescheduleRequestStatus.Cancelled, student.FirstName, cancellationToken))
            return false;

        await notifier.UpdateAdminsAsync(request.AdminMessages, ToInfo(request), "🚫 Учень скасував запит", cancellationToken);
        return true;
    }

    public async Task<RescheduleDecision> DecideAsync(Guid requestId, bool approve, string decidedBy, CancellationToken cancellationToken = default)
    {
        var request = await LoadRequestAsync(requestId, cancellationToken);
        if (request is null)
            return RescheduleDecision.NotFound;

        if (request.Status != RescheduleRequestStatus.Pending)
            return RescheduleDecision.AlreadyDecided;

        var info = ToInfo(request);

        if (!approve)
        {
            if (!await TryCloseAsync(requestId, RescheduleRequestStatus.Declined, decidedBy, cancellationToken))
                return RescheduleDecision.AlreadyDecided;

            await AnnounceDeclineAsync(request, info, $"❌ Відхилено — {decidedBy}", slotTaken: false, cancellationToken);
            LogDecided(logger, requestId, RescheduleDecision.Declined, decidedBy);
            return RescheduleDecision.Declined;
        }

        var outcome = await ApproveAsync(request, decidedBy, cancellationToken);
        switch (outcome)
        {
            case RescheduleDecision.Approved:
                await notifier.UpdateAdminsAsync(request.AdminMessages, info, $"✅ Погоджено — {decidedBy}", cancellationToken);
                await notifier.NotifyStudentApprovedAsync(info, cancellationToken);
                break;
            case RescheduleDecision.SlotTaken:
                await AnnounceDeclineAsync(request, info, "⚠️ Цей час уже зайнятий, учня попрошено обрати інший", slotTaken: true, cancellationToken);
                break;
        }

        LogDecided(logger, requestId, outcome, decidedBy);
        return outcome;
    }

    /// <summary>
    /// The student's next lessons that can be moved, for /lessons.
    /// </summary>
    public async Task<IReadOnlyList<UpcomingLesson>> ListUpcomingAsync(long telegramUserId, int count, CancellationToken cancellationToken)
    {
        var now = clock.Now;
        var today = DateOnly.FromDateTime(now);
        var time = TimeOnly.FromDateTime(now);

        // a student sees their own lessons, a parent those of their children
        return await context.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.Student != null
                             && lesson.Status == LessonStatus.Scheduled
                             && (lesson.Date > today || (lesson.Date == today && lesson.StartTime > time)))
            .Where(lesson => (lesson.Student!.TelegramUserId == telegramUserId && !lesson.Student.IsParent)
                             || lesson.Student!.Parents.Any(parent => parent.IsParent && parent.TelegramUserId == telegramUserId))
            .OrderBy(lesson => lesson.Date)
            .ThenBy(lesson => lesson.StartTime)
            .Take(count)
            .Select(lesson => new UpcomingLesson(
                lesson.Id,
                lesson.Date,
                lesson.StartTime,
                lesson.Room.Name,
                lesson.Teacher.Name,
                lesson.RescheduleRequests.Any(request => request.Status == RescheduleRequestStatus.Pending),
                lesson.Student!.TelegramUserId == telegramUserId ? null : lesson.Student.FirstName))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> IsAdminAsync(long telegramUserId, CancellationToken cancellationToken) =>
        context.TelegramSubscribers.AnyAsync(subscriber => subscriber.TelegramUserId == telegramUserId && subscriber.IsAdmin, cancellationToken);

    private async Task<RescheduleDecision> ApproveAsync(LessonRescheduleRequest request, string decidedBy, CancellationToken cancellationToken)
    {
        var lesson = request.Lesson;
        var proposedStart = request.ProposedDate.ToDateTime(request.ProposedTime);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var clash = await LessonClashes.FindAsync(
            context, lesson.Id, request.ProposedDate, request.ProposedTime, request.ProposedRoomId, lesson.TeacherId, lesson.StudentId, cancellationToken);

        // taken since the student proposed it, or it is in the past by now
        if (clash is not null || lesson.Status != LessonStatus.Scheduled || proposedStart <= clock.Now)
        {
            if (!await TryCloseAsync(request.Id, RescheduleRequestStatus.Declined, decidedBy, cancellationToken))
                return RescheduleDecision.AlreadyDecided;

            await transaction.CommitAsync(cancellationToken);
            return RescheduleDecision.SlotTaken;
        }

        // the conditional update is the lock: of two admins pressing "yes" at once only one gets here
        if (!await TryCloseAsync(request.Id, RescheduleRequestStatus.Approved, decidedBy, cancellationToken))
            return RescheduleDecision.AlreadyDecided;

        var moved = new Lesson
        {
            Id = Guid.CreateVersion7(),
            Date = request.ProposedDate,
            StartTime = request.ProposedTime,
            RoomId = request.ProposedRoomId,
            TeacherId = lesson.TeacherId,
            StudentId = lesson.StudentId,
            Note = lesson.Note,
            Price = lesson.Price,
            // the payment goes with the lesson; the old one counts nowhere any more
            IsPaid = lesson.IsPaid,
            PaidAtUtc = lesson.PaidAtUtc,
            Status = LessonStatus.Scheduled,
            CreatedAtUtc = clock.UtcNow
        };

        context.Lessons.Add(moved);
        lesson.Status = LessonStatus.Rescheduled;
        lesson.RescheduledToId = moved.Id;
        lesson.IsPaid = false;
        lesson.PaidAtUtc = null;

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RescheduleDecision.Approved;
    }

    private async Task AnnounceDeclineAsync(
        LessonRescheduleRequest request,
        RescheduleRequestInfo info,
        string adminOutcome,
        bool slotTaken,
        CancellationToken cancellationToken)
    {
        await notifier.UpdateAdminsAsync(request.AdminMessages, info, adminOutcome, cancellationToken);

        // fresh times to pick from, the declined one now among the crossed out
        context.ChangeTracker.Clear();
        var lesson = await LoadLessonAsync(request.LessonId, cancellationToken);
        var options = lesson is not null && IsMovable(lesson) ? await BuildOptionsAsync(lesson, cancellationToken) : null;

        await notifier.NotifyStudentDeclinedAsync(info, slotTaken, options, cancellationToken);
    }

    /// <summary>
    /// Pending → <paramref name="status"/>, only if it is still pending.
    /// </summary>
    private async Task<bool> TryCloseAsync(Guid requestId, RescheduleRequestStatus status, string decidedBy, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var updated = await context.LessonRescheduleRequests
            .Where(request => request.Id == requestId && request.Status == RescheduleRequestStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(request => request.Status, status)
                .SetProperty(request => request.DecidedAtUtc, now)
                .SetProperty(request => request.DecidedBy, decidedBy), cancellationToken);

        return updated > 0;
    }

    private async Task<RescheduleOptions> BuildOptionsAsync(Lesson lesson, CancellationToken cancellationToken)
    {
        var now = clock.Now;
        var earliest = now.AddHours(_settings.RescheduleMinNoticeHours);
        var firstDate = DateOnly.FromDateTime(earliest);
        var lastDate = DateOnly.FromDateTime(now).AddDays(_settings.RescheduleDaysAhead);

        var rooms = await context.Rooms
            .AsNoTracking()
            .Include(room => room.Hours)
            .OrderBy(room => room.SortOrder)
            .ThenBy(room => room.Name)
            .ToListAsync(cancellationToken);

        // the lesson's own room is offered first
        rooms = [.. rooms.OrderBy(room => room.Id == lesson.RoomId ? 0 : 1)];

        // every lesson of the period: the teacher's and the student's for them, the others for the rooms
        var busy = await context.Lessons
            .AsNoTracking()
            .Where(other => other.Date >= firstDate
                            && other.Date <= lastDate
                            && other.Status == LessonStatus.Scheduled
                            && other.Id != lesson.Id)
            .Select(other => new { other.Date, other.StartTime, other.RoomId, other.TeacherId, other.StudentId })
            .ToListAsync(cancellationToken);

        var slots = new List<FreeSlot>();

        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            var day = date;
            var dayBusy = busy.Where(other => other.Date == day).ToList();
            var openRooms = rooms
                .Select(room => (Room: room, Hours: room.Hours.FirstOrDefault(hours => hours.DayOfWeek == day.DayOfWeek)))
                .Where(entry => entry.Hours is not null)
                .ToList();

            var times = openRooms
                .SelectMany(entry => HourRange(entry.Hours!.FirstLessonAt, entry.Hours.LastLessonAt))
                .Distinct()
                .Order();

            foreach (var time in times)
            {
                if (day.ToDateTime(time) < earliest || (day == lesson.Date && time == lesson.StartTime))
                    continue;

                if (dayBusy.Any(other => Overlaps(other.StartTime, time)
                                         && (other.TeacherId == lesson.TeacherId
                                             || (lesson.StudentId != null && other.StudentId == lesson.StudentId))))
                    continue;

                var room = openRooms.FirstOrDefault(entry =>
                    time >= entry.Hours!.FirstLessonAt
                    && time <= entry.Hours.LastLessonAt
                    && !dayBusy.Any(other => other.RoomId == entry.Room.Id && Overlaps(other.StartTime, time)));

                if (room.Room is not null)
                    slots.Add(new FreeSlot(day, time, room.Room.Id, room.Room.Name));
            }
        }

        var declined = lesson.RescheduleRequests
            .Where(request => request.Status == RescheduleRequestStatus.Declined)
            .Select(request => (request.ProposedDate, request.ProposedTime))
            .ToHashSet();

        return new RescheduleOptions(ToRescheduleLesson(lesson), slots, declined, _settings.RescheduleDaysAhead);
    }

    private bool IsMovable(Lesson lesson) =>
        lesson.Status == LessonStatus.Scheduled && lesson.Date.ToDateTime(lesson.StartTime) > clock.Now;

    private async Task<Lesson?> FindStudentLessonAsync(Guid lessonId, long telegramUserId, CancellationToken cancellationToken)
    {
        var lesson = await LoadLessonAsync(lessonId, cancellationToken);
        return lesson?.Student?.TelegramUserId == telegramUserId ? lesson : null;
    }

    private Task<Lesson?> LoadLessonAsync(Guid lessonId, CancellationToken cancellationToken) =>
        context.Lessons
            .Include(lesson => lesson.Student)
            .Include(lesson => lesson.Room)
            .Include(lesson => lesson.Teacher)
            .Include(lesson => lesson.RescheduleRequests)
            .AsSplitQuery()
            .FirstOrDefaultAsync(lesson => lesson.Id == lessonId, cancellationToken);

    private Task<LessonRescheduleRequest?> LoadRequestAsync(Guid requestId, CancellationToken cancellationToken) =>
        context.LessonRescheduleRequests
            .Include(request => request.ProposedRoom)
            .Include(request => request.Lesson).ThenInclude(lesson => lesson.Student)
            .Include(request => request.Lesson).ThenInclude(lesson => lesson.Room)
            .Include(request => request.Lesson).ThenInclude(lesson => lesson.Teacher)
            .FirstOrDefaultAsync(request => request.Id == requestId, cancellationToken);

    private static RescheduleRequestInfo ToInfo(LessonRescheduleRequest request) => new(
        request.Id,
        ToRescheduleLesson(request.Lesson),
        request.ProposedDate,
        request.ProposedTime,
        request.ProposedRoom.Name,
        request.StudentChatId);

    private static RescheduleLesson ToRescheduleLesson(Lesson lesson) => new(
        lesson.Id,
        lesson.Student is null
            ? lesson.Note ?? "—"
            : string.Join(' ', new[] { lesson.Student.FirstName, lesson.Student.LastName }.Where(part => !string.IsNullOrEmpty(part))),
        lesson.Student?.Username,
        lesson.Date,
        lesson.StartTime,
        lesson.Room.Name,
        lesson.Teacher.Name);

    // TimeSpan, not TimeOnly subtraction: the latter wraps around midnight
    private static bool Overlaps(TimeOnly other, TimeOnly time) =>
        Math.Abs((other.ToTimeSpan() - time.ToTimeSpan()).TotalMinutes) < LessonDto.DurationMinutes;

    private static IEnumerable<TimeOnly> HourRange(TimeOnly first, TimeOnly last)
    {
        for (var hour = first.Hour; hour <= last.Hour; hour++)
            yield return new TimeOnly(hour, 0);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reschedule of lesson {LessonId} to {Date} {Time} proposed, {AdminCount} admins notified")]
    private static partial void LogProposed(ILogger logger, Guid lessonId, DateOnly date, TimeOnly time, int adminCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reschedule request {RequestId}: {Decision} by {DecidedBy}")]
    private static partial void LogDecided(ILogger logger, Guid requestId, RescheduleDecision decision, string decidedBy);
}
