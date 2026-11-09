using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Lessons;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Lessons;

public class LessonService(ApplicationDbContext context, SchoolClock clock, LessonCancellationService cancellations) : ILessonService
{
    private static readonly TimeSpan LessonDuration = TimeSpan.FromMinutes(LessonDto.DurationMinutes);

    private static readonly Expression<Func<Lesson, LessonDto>> ToDto = lesson => new LessonDto(
        lesson.Id,
        lesson.Date,
        lesson.StartTime,
        lesson.RoomId,
        lesson.TeacherId,
        lesson.StudentId,
        lesson.Student == null
            ? null
            : lesson.Student.LastName == null
                ? lesson.Student.FirstName
                : lesson.Student.FirstName + " " + lesson.Student.LastName,
        lesson.Student == null ? null : (bool?)lesson.Student.IsActive,
        lesson.Note,
        lesson.Price,
        lesson.IsPaid,
        lesson.PaidAtUtc,
        lesson.Status,
        lesson.RescheduledToId,
        lesson.RescheduleRequests
            .Where(request => request.Status == RescheduleRequestStatus.Pending)
            .Select(request => new PendingRescheduleDto(
                request.Id, request.ProposedDate, request.ProposedTime, request.ProposedRoomId, request.CreatedAtUtc))
            .FirstOrDefault(),
        lesson.Status == LessonStatus.Cancelled && lesson.CancelledAtUtc != null && lesson.CancelledBy != null
            ? new LessonCancellationDto(
                lesson.CancelledAtUtc.Value, lesson.CancelledBy.Value, lesson.CancelledByName, lesson.CancellationReason, lesson.IsLateCancellation)
            : null,
        lesson.SeriesId);

    public async Task<WeekScheduleDto> GetWeekAsync(DateOnly? day, CancellationToken cancellationToken = default)
    {
        var today = clock.Today;
        var weekStart = SchoolClock.StartOfWeek(day ?? today);
        var weekEnd = weekStart.AddDays(6);

        var rooms = await context.Rooms
            .AsNoTracking()
            .OrderBy(room => room.SortOrder)
            .ThenBy(room => room.Name)
            .Select(room => new RoomDto(
                room.Id,
                room.Name,
                room.Hours
                    .OrderBy(hours => hours.DayOfWeek)
                    .Select(hours => new RoomHoursDto(hours.DayOfWeek, hours.FirstLessonAt, hours.LastLessonAt))
                    .ToList()))
            .ToListAsync(cancellationToken);

        var teachers = await ListTeachersAsync(cancellationToken);

        var lessons = await context.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.Date >= weekStart && lesson.Date <= weekEnd)
            .OrderBy(lesson => lesson.Date)
            .ThenBy(lesson => lesson.StartTime)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

        return new WeekScheduleDto(weekStart, weekEnd, today, rooms, teachers, lessons);
    }

    public async Task<IReadOnlyList<StudentOptionDto>> ListStudentsAsync(CancellationToken cancellationToken = default) =>
        await context.TelegramSubscribers
            .AsNoTracking()
            // parents get broadcasts, not lessons
            .Where(subscriber => !subscriber.IsParent)
            .OrderBy(subscriber => subscriber.FirstName)
            .ThenBy(subscriber => subscriber.LastName)
            .Select(subscriber => new StudentOptionDto(
                subscriber.Id,
                subscriber.LastName == null ? subscriber.FirstName : subscriber.FirstName + " " + subscriber.LastName,
                subscriber.Username,
                subscriber.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LessonDto>> CreateAsync(LessonInput input, int repeatWeeks, CancellationToken cancellationToken = default)
    {
        if (repeatWeeks is < 1 or > LessonDto.MaxRepeatWeeks)
            throw new ValidationFailedException("RepeatWeeks", $"Repeat the lesson for 1 to {LessonDto.MaxRepeatWeeks} weeks.");

        var note = Validate(input);
        await EnsureReferencesExistAsync(input, cancellationToken);

        // every week is checked before anything is stored: either the whole series is created or none of it
        var dates = Enumerable.Range(0, repeatWeeks).Select(week => input.Date.AddDays(7 * week)).ToList();
        foreach (var date in dates)
            await EnsureNoClashAsync([], input with { Date = date }, cancellationToken);

        var now = clock.UtcNow;
        Guid? seriesId = repeatWeeks > 1 ? Guid.CreateVersion7() : null;
        var lessons = dates.Select(date => new Lesson
        {
            Id = Guid.CreateVersion7(),
            Date = date,
            StartTime = input.StartTime,
            RoomId = input.RoomId,
            TeacherId = input.TeacherId,
            StudentId = input.StudentId,
            Note = note,
            SeriesId = seriesId,
            Price = input.Price,
            // a series is paid lesson by lesson, only the first one takes the flag
            IsPaid = input.IsPaid && date == input.Date,
            PaidAtUtc = input.IsPaid && date == input.Date ? now : null,
            CreatedAtUtc = now
        }).ToList();

        context.Lessons.AddRange(lessons);
        await context.SaveChangesAsync(cancellationToken);

        var ids = lessons.Select(lesson => lesson.Id).ToList();
        return await context.Lessons
            .AsNoTracking()
            .Where(lesson => ids.Contains(lesson.Id))
            .OrderBy(lesson => lesson.Date)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    public async Task<CopyWeekResultDto> CopyPreviousWeekAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        var weekStart = SchoolClock.StartOfWeek(day);
        var weekEnd = weekStart.AddDays(6);
        var sourceStart = weekStart.AddDays(-7);
        var sourceEnd = weekStart.AddDays(-1);

        var sources = await context.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.Date >= sourceStart && lesson.Date <= sourceEnd)
            // a lesson that replaced a rescheduled one is a one-off move; the original is the regular slot
            .Where(lesson => !context.Lessons.Any(original => original.RescheduledToId == lesson.Id))
            // when two of them clash (a slot freed by a reschedule got booked), the lesson that took place wins
            .OrderBy(lesson => lesson.Status != LessonStatus.Scheduled)
            .ThenBy(lesson => lesson.Date)
            .ThenBy(lesson => lesson.StartTime)
            .Select(lesson => new
            {
                lesson.Date,
                lesson.StartTime,
                lesson.RoomId,
                lesson.TeacherId,
                lesson.StudentId,
                lesson.Note,
                lesson.Price,
                lesson.SeriesId,
                Title = lesson.Student == null
                    ? lesson.Note
                    : lesson.Student.LastName == null
                        ? lesson.Student.FirstName
                        : lesson.Student.FirstName + " " + lesson.Student.LastName
            })
            .ToListAsync(cancellationToken);

        // any status: a lesson cancelled or moved in this week must not come back as a copy
        var existing = await context.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.Date >= weekStart && lesson.Date <= weekEnd)
            .Select(lesson => new { lesson.Date, lesson.StartTime, lesson.RoomId, lesson.TeacherId, lesson.StudentId })
            .ToListAsync(cancellationToken);

        var now = clock.Now;
        var utcNow = clock.UtcNow;
        var created = new List<Lesson>();
        var skipped = new List<SkippedLessonCopyDto>();

        foreach (var source in sources)
        {
            var date = source.Date.AddDays(7);
            void Skip(CopySkipReason reason, string? detail = null) =>
                skipped.Add(new SkippedLessonCopyDto(date, source.StartTime, source.Title, reason, detail));

            if (existing.Any(lesson => lesson.Date == date
                                       && lesson.StartTime == source.StartTime
                                       && lesson.RoomId == source.RoomId
                                       && lesson.TeacherId == source.TeacherId
                                       && lesson.StudentId == source.StudentId))
            {
                Skip(CopySkipReason.AlreadyExists);
                continue;
            }

            // an unpaid lesson that has started counts as a debt, so the past of the week is not filled in
            if (date.ToDateTime(source.StartTime) <= now)
            {
                Skip(CopySkipReason.InThePast);
                continue;
            }

            var clash = await LessonClashes.FindAsync(
                context, Array.Empty<Guid>(), date, source.StartTime, source.RoomId, source.TeacherId, source.StudentId, cancellationToken);
            if (clash is not null)
            {
                Skip(CopySkipReason.Clash, clash);
                continue;
            }

            // the copies are not stored yet, so they are checked against each other here
            var copyClash = created.FirstOrDefault(other => other.Date == date
                                                           // ticks, not TimeOnly subtraction: that one wraps around midnight
                                                           && Math.Abs(other.StartTime.Ticks - source.StartTime.Ticks) < LessonDuration.Ticks
                                                           && (other.RoomId == source.RoomId
                                                               || other.TeacherId == source.TeacherId
                                                               || (source.StudentId != null && other.StudentId == source.StudentId)));
            if (copyClash is not null)
            {
                Skip(CopySkipReason.Clash, $"Clashes with another copied lesson at {copyClash.StartTime:HH\\:mm}.");
                continue;
            }

            created.Add(new Lesson
            {
                Id = Guid.CreateVersion7(),
                Date = date,
                StartTime = source.StartTime,
                RoomId = source.RoomId,
                TeacherId = source.TeacherId,
                StudentId = source.StudentId,
                Note = source.Note,
                Price = source.Price,
                // the copy continues the series, so "this and the following" reaches it too
                SeriesId = source.SeriesId,
                CreatedAtUtc = utcNow
            });
        }

        if (created.Count > 0)
        {
            context.Lessons.AddRange(created);
            await context.SaveChangesAsync(cancellationToken);
        }

        var ids = created.Select(lesson => lesson.Id).ToList();
        var lessons = await context.Lessons
            .AsNoTracking()
            .Where(lesson => ids.Contains(lesson.Id))
            .OrderBy(lesson => lesson.Date)
            .ThenBy(lesson => lesson.StartTime)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

        return new CopyWeekResultDto(
            lessons,
            skipped.OrderBy(lesson => lesson.Date).ThenBy(lesson => lesson.StartTime).ToList());
    }

    public async Task<LessonDto?> UpdateAsync(Guid id, LessonInput input, LessonChangeScope scope = LessonChangeScope.This,
        CancellationToken cancellationToken = default)
    {
        var lesson = await context.Lessons.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (lesson is null)
            return null;

        EnsureEditable(lesson);

        var note = Validate(input);
        await EnsureReferencesExistAsync(input, cancellationToken);

        var following = scope == LessonChangeScope.Following
            ? await FollowingInSeries(lesson)
                .Where(other => other.Status == LessonStatus.Scheduled)
                .ToListAsync(cancellationToken)
            : new List<Lesson>();
        // ones whose time has come are history, like a lesson that took place
        var now = clock.Now;
        following = following.Where(other => other.Date.ToDateTime(other.StartTime) > now).ToList();

        // the series moves by as many days as this lesson does, keeping one lesson a week
        var dayShift = input.Date.DayNumber - lesson.Date.DayNumber;
        var movingIds = following.Select(other => other.Id).Append(id).ToList();

        // every lesson is checked before anything is stored, like creating a series: all or nothing
        await EnsureNoClashAsync(movingIds, input, cancellationToken);
        foreach (var other in following)
            await EnsureNoClashAsync(movingIds, input with { Date = other.Date.AddDays(dayShift) }, cancellationToken);

        foreach (var other in following)
        {
            other.Date = other.Date.AddDays(dayShift);
            other.StartTime = input.StartTime;
            other.RoomId = input.RoomId;
            other.TeacherId = input.TeacherId;
            other.StudentId = input.StudentId;
            other.Note = note;
            other.Price = input.Price;
        }

        lesson.Date = input.Date;
        lesson.StartTime = input.StartTime;
        lesson.RoomId = input.RoomId;
        lesson.TeacherId = input.TeacherId;
        lesson.StudentId = input.StudentId;
        lesson.Note = note;
        lesson.Price = input.Price;
        SetPaid(lesson, input.IsPaid);

        await context.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<LessonDto?> SetPaidAsync(Guid id, bool isPaid, CancellationToken cancellationToken = default)
    {
        var lesson = await context.Lessons.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (lesson is null)
            return null;

        EnsureEditable(lesson);

        SetPaid(lesson, isPaid);
        await context.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, LessonChangeScope scope = LessonChangeScope.This, CancellationToken cancellationToken = default)
    {
        var lesson = await context.Lessons.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (lesson is null)
            return false;

        List<Guid> ids = [id];
        if (scope == LessonChangeScope.Following)
        {
            var today = clock.Today;
            var following = await FollowingInSeries(lesson)
                // the past keeps its history (payments, statistics) even when the series ends
                .Where(other => other.Date >= today)
                .Select(other => new { other.Id, other.RescheduledToId })
                .ToListAsync(cancellationToken);

            ids.AddRange(following.Select(other => other.Id));
            // a later lesson moved by a reschedule is still a lesson of this series
            ids.AddRange(following.Where(other => other.RescheduledToId != null).Select(other => other.RescheduledToId!.Value));
            if (lesson.RescheduledToId is { } movedTo)
                ids.Add(movedTo);
            ids = ids.Distinct().ToList();
        }

        // the database cannot clear the self reference itself (no SET NULL on a cycle)
        await context.Lessons
            .Where(other => other.RescheduledToId != null && ids.Contains(other.RescheduledToId.Value))
            .ExecuteUpdateAsync(setters => setters.SetProperty(other => other.RescheduledToId, (Guid?)null), cancellationToken);

        // their reminders and reschedule requests go with them (cascade)
        return await context.Lessons.Where(other => ids.Contains(other.Id)).ExecuteDeleteAsync(cancellationToken) > 0;
    }

    /// <summary>
    /// The later lessons of the lesson's series, any status; none for a lesson outside a series.
    /// </summary>
    private IQueryable<Lesson> FollowingInSeries(Lesson lesson) =>
        lesson.SeriesId is { } seriesId
            ? context.Lessons.Where(other => other.SeriesId == seriesId && other.Date > lesson.Date)
            : context.Lessons.Where(_ => false);

    public async Task<LessonDto?> CancelAsync(Guid id, string? reason, string? cancelledBy, CancellationToken cancellationToken = default)
    {
        var (lesson, problem) = await cancellations.CancelAsync(id, CancellationSource.Admin, cancelledBy, reason, cancellationToken);

        return problem switch
        {
            CancellationProblem.NotFound => null,
            CancellationProblem.NotCancellable => throw new ValidationFailedException(
                nameof(LessonDto.Status), "Only a lesson that has not started and is not cancelled or rescheduled can be cancelled."),
            _ => await GetAsync(lesson!.LessonId, cancellationToken)
        };
    }

    public async Task<LessonDto?> RestoreAsync(Guid id, CancellationToken cancellationToken = default) =>
        await cancellations.RestoreAsync(id, cancellationToken) ? await GetAsync(id, cancellationToken) : null;

    private Task<LessonDto?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Lessons.AsNoTracking().Where(lesson => lesson.Id == id).Select(ToDto).FirstOrDefaultAsync(cancellationToken);

    private async Task<IReadOnlyList<TeacherDto>> ListTeachersAsync(CancellationToken cancellationToken) =>
        await context.Teachers
            .AsNoTracking()
            .OrderBy(teacher => teacher.SortOrder)
            .ThenBy(teacher => teacher.Name)
            .Select(teacher => new TeacherDto(teacher.Id, teacher.Name, teacher.Color))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Keeps the time it was paid when an already paid lesson is saved again.
    /// </summary>
    private void SetPaid(Lesson lesson, bool isPaid)
    {
        if (lesson.IsPaid == isPaid)
            return;

        lesson.IsPaid = isPaid;
        lesson.PaidAtUtc = isPaid ? clock.UtcNow : null;
    }

    /// <returns>The trimmed note, null when empty.</returns>
    private static string? Validate(LessonInput input)
    {
        var errors = new Dictionary<string, string[]>();
        var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();

        if (input.StartTime < LessonDto.EarliestStart || input.StartTime > LessonDto.LatestStart)
            errors[nameof(LessonInput.StartTime)] = [$"A lesson starts between {LessonDto.EarliestStart:HH\\:mm} and {LessonDto.LatestStart:HH\\:mm}."];

        if (input.Price is < 0 or > LessonDto.MaxPrice)
            errors[nameof(LessonInput.Price)] = [$"The price must be between 0 and {LessonDto.MaxPrice:0}."];

        if (note?.Length > LessonDto.NoteMaxLength)
            errors[nameof(LessonInput.Note)] = [$"The note exceeds {LessonDto.NoteMaxLength} characters."];

        if (input.StudentId is null && note is null)
            errors[nameof(LessonInput.StudentId)] = ["Choose a student or write a note, e.g. \"trial lesson\"."];

        if (errors.Count > 0)
            throw new ValidationFailedException(errors);

        return note;
    }

    private async Task EnsureReferencesExistAsync(LessonInput input, CancellationToken cancellationToken)
    {
        if (!await context.Rooms.AnyAsync(room => room.Id == input.RoomId, cancellationToken))
            throw new ValidationFailedException(nameof(LessonInput.RoomId), "The room does not exist.");

        if (!await context.Teachers.AnyAsync(teacher => teacher.Id == input.TeacherId, cancellationToken))
            throw new ValidationFailedException(nameof(LessonInput.TeacherId), "The teacher does not exist.");

        if (input.StudentId is { } studentId
            && !await context.TelegramSubscribers.AnyAsync(subscriber => subscriber.Id == studentId, cancellationToken))
            throw new ValidationFailedException(nameof(LessonInput.StudentId), "The student does not exist.");
    }

    /// <param name="movingLessonIds">The lessons being changed, they do not clash with their own old places.</param>
    private async Task EnsureNoClashAsync(IReadOnlyCollection<Guid> movingLessonIds, LessonInput input, CancellationToken cancellationToken)
    {
        var clash = await LessonClashes.FindAsync(
            context, movingLessonIds, input.Date, input.StartTime, input.RoomId, input.TeacherId, input.StudentId, cancellationToken);

        if (clash is not null)
            throw new ValidationFailedException(nameof(LessonInput.StartTime), clash);
    }

    // a moved lesson is history: the lesson that replaced it is the one to change; a cancelled one is restored first
    private static void EnsureEditable(Lesson lesson)
    {
        if (lesson.Status == LessonStatus.Rescheduled)
            throw new ValidationFailedException(nameof(LessonDto.Status), "The lesson was rescheduled, change the lesson that replaced it.");

        if (lesson.Status == LessonStatus.Cancelled)
            throw new ValidationFailedException(nameof(LessonDto.Status), "The lesson is cancelled, restore it first.");
    }
}
