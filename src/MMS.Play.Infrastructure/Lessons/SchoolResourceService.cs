using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Lessons;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Lessons;

public partial class SchoolResourceService(ApplicationDbContext context) : ISchoolResourceService
{
    // Monday first, as the school week and the timetable go
    private static readonly DayOfWeek[] WeekOrder =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    public async Task<IReadOnlyList<TeacherSummaryDto>> ListTeachersAsync(CancellationToken cancellationToken = default) =>
        await context.Teachers
            .AsNoTracking()
            .OrderBy(teacher => teacher.SortOrder)
            .ThenBy(teacher => teacher.Name)
            .Select(teacher => new TeacherSummaryDto(
                teacher.Id,
                teacher.Name,
                teacher.Color,
                context.Lessons.Count(lesson => lesson.TeacherId == teacher.Id)))
            .ToListAsync(cancellationToken);

    public async Task<TeacherSummaryDto> CreateTeacherAsync(TeacherInput input, CancellationToken cancellationToken = default)
    {
        var (name, color) = await ValidateAsync(null, input, cancellationToken);

        var teacher = new Teacher
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Color = color,
            SortOrder = await NextSortOrderAsync(context.Teachers.Select(x => x.SortOrder), cancellationToken)
        };
        context.Teachers.Add(teacher);
        await context.SaveChangesAsync(cancellationToken);

        return new TeacherSummaryDto(teacher.Id, teacher.Name, teacher.Color, 0);
    }

    public async Task<TeacherSummaryDto?> UpdateTeacherAsync(Guid id, TeacherInput input, CancellationToken cancellationToken = default)
    {
        var teacher = await context.Teachers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (teacher is null)
            return null;

        (teacher.Name, teacher.Color) = await ValidateAsync(id, input, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var lessonCount = await context.Lessons.CountAsync(lesson => lesson.TeacherId == id, cancellationToken);
        return new TeacherSummaryDto(teacher.Id, teacher.Name, teacher.Color, lessonCount);
    }

    public async Task<bool> DeleteTeacherAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var teacher = await context.Teachers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (teacher is null)
            return false;

        var lessonCount = await context.Lessons.CountAsync(lesson => lesson.TeacherId == id, cancellationToken);
        if (lessonCount > 0)
        {
            throw new ValidationFailedException(
                "Id",
                $"{teacher.Name} has {lessonCount} lesson(s). Give them to another teacher or delete them first: lessons and earnings refer to the teacher.");
        }

        context.Teachers.Remove(teacher);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<RoomSummaryDto>> ListRoomsAsync(CancellationToken cancellationToken = default)
    {
        var rooms = await context.Rooms
            .AsNoTracking()
            .OrderBy(room => room.SortOrder)
            .ThenBy(room => room.Name)
            .Select(room => new
            {
                room.Id,
                room.Name,
                Hours = room.Hours.Select(hours => new RoomHoursDto(hours.DayOfWeek, hours.FirstLessonAt, hours.LastLessonAt)).ToList(),
                LessonCount = context.Lessons.Count(lesson => lesson.RoomId == room.Id)
            })
            .ToListAsync(cancellationToken);

        return rooms.Select(room => new RoomSummaryDto(room.Id, room.Name, InWeekOrder(room.Hours), room.LessonCount)).ToList();
    }

    public async Task<RoomSummaryDto> CreateRoomAsync(RoomInput input, CancellationToken cancellationToken = default)
    {
        var (name, hours) = await ValidateAsync(null, input, cancellationToken);

        var room = new Room
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            SortOrder = await NextSortOrderAsync(context.Rooms.Select(x => x.SortOrder), cancellationToken),
            Hours = hours.Select(ToEntity).ToList()
        };
        context.Rooms.Add(room);
        await context.SaveChangesAsync(cancellationToken);

        return new RoomSummaryDto(room.Id, room.Name, InWeekOrder(hours), 0);
    }

    public async Task<RoomSummaryDto?> UpdateRoomAsync(Guid id, RoomInput input, CancellationToken cancellationToken = default)
    {
        var room = await context.Rooms.Include(x => x.Hours).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (room is null)
            return null;

        var (name, hours) = await ValidateAsync(id, input, cancellationToken);
        room.Name = name;

        // the key is (room, day): update the days that stay, so EF does not delete and insert the same key
        room.Hours.RemoveAll(existing => hours.All(day => day.DayOfWeek != existing.DayOfWeek));
        foreach (var day in hours)
        {
            if (room.Hours.FirstOrDefault(existing => existing.DayOfWeek == day.DayOfWeek) is { } existing)
            {
                existing.FirstLessonAt = day.FirstLessonAt;
                existing.LastLessonAt = day.LastLessonAt;
            }
            else
            {
                room.Hours.Add(ToEntity(day));
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        var lessonCount = await context.Lessons.CountAsync(lesson => lesson.RoomId == id, cancellationToken);
        return new RoomSummaryDto(room.Id, room.Name, InWeekOrder(hours), lessonCount);
    }

    public async Task<bool> DeleteRoomAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var room = await context.Rooms.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (room is null)
            return false;

        var lessonCount = await context.Lessons.CountAsync(lesson => lesson.RoomId == id, cancellationToken);
        if (lessonCount > 0)
        {
            throw new ValidationFailedException(
                "Id",
                $"Room {room.Name} has {lessonCount} lesson(s). Move them to another room or delete them first.");
        }

        if (await context.LessonRescheduleRequests.AnyAsync(request => request.ProposedRoomId == id, cancellationToken))
            throw new ValidationFailedException("Id", $"Room {room.Name} is part of a reschedule request.");

        // the hours go with the room (cascade)
        context.Rooms.Remove(room);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<(string Name, string Color)> ValidateAsync(Guid? id, TeacherInput input, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var name = (input.Name ?? string.Empty).Trim();
        var color = (input.Color ?? string.Empty).Trim().ToLowerInvariant();

        if (name.Length == 0)
            errors[nameof(TeacherInput.Name)] = ["Write the name."];
        else if (name.Length > TeacherSummaryDto.NameMaxLength)
            errors[nameof(TeacherInput.Name)] = [$"The name can be at most {TeacherSummaryDto.NameMaxLength} characters long."];
        else if (await context.Teachers.AnyAsync(teacher => teacher.Name == name && teacher.Id != id, cancellationToken))
            errors[nameof(TeacherInput.Name)] = [$"There is a teacher named {name} already."];

        if (!HexColor().IsMatch(color))
            errors[nameof(TeacherInput.Color)] = ["The color must look like \"#22c55e\"."];

        if (errors.Count > 0)
            throw new ValidationFailedException(errors);

        return (name, color);
    }

    private async Task<(string Name, List<RoomHoursDto> Hours)> ValidateAsync(Guid? id, RoomInput input, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var name = (input.Name ?? string.Empty).Trim();
        var hours = (input.Hours ?? []).ToList();

        if (name.Length == 0)
            errors[nameof(RoomInput.Name)] = ["Write the room number or name."];
        else if (name.Length > RoomSummaryDto.NameMaxLength)
            errors[nameof(RoomInput.Name)] = [$"The name can be at most {RoomSummaryDto.NameMaxLength} characters long."];
        else if (await context.Rooms.AnyAsync(room => room.Name == name && room.Id != id, cancellationToken))
            errors[nameof(RoomInput.Name)] = [$"There is a room {name} already."];

        if (hours.GroupBy(day => day.DayOfWeek).Any(day => day.Count() > 1))
            errors[nameof(RoomInput.Hours)] = ["Every day can be listed once."];
        else if (hours.Any(day => !Enum.IsDefined(day.DayOfWeek)))
            errors[nameof(RoomInput.Hours)] = ["Unknown day of the week."];
        else if (hours.Any(day => day.FirstLessonAt > day.LastLessonAt))
            errors[nameof(RoomInput.Hours)] = ["The first lesson of a day cannot start after the last one."];
        else if (hours.Any(day => day.FirstLessonAt < LessonDto.EarliestStart || day.LastLessonAt > LessonDto.LatestStart))
            errors[nameof(RoomInput.Hours)] = [$"Lessons start between {LessonDto.EarliestStart:HH\\:mm} and {LessonDto.LatestStart:HH\\:mm}."];

        if (errors.Count > 0)
            throw new ValidationFailedException(errors);

        return (name, hours);
    }

    private static async Task<int> NextSortOrderAsync(IQueryable<int> sortOrders, CancellationToken cancellationToken) =>
        (await sortOrders.Select(order => (int?)order).MaxAsync(cancellationToken) ?? 0) + 1;

    private static RoomHours ToEntity(RoomHoursDto day) => new()
    {
        DayOfWeek = day.DayOfWeek,
        FirstLessonAt = day.FirstLessonAt,
        LastLessonAt = day.LastLessonAt
    };

    private static List<RoomHoursDto> InWeekOrder(IEnumerable<RoomHoursDto> hours) =>
        hours.OrderBy(day => Array.IndexOf(WeekOrder, day.DayOfWeek)).ToList();

    [GeneratedRegex("^#[0-9a-f]{6}$")]
    private static partial Regex HexColor();
}
