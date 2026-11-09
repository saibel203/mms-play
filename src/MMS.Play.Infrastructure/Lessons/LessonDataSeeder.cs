using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Lessons;

/// <summary>
/// Creates the teachers and the rooms with their hours when there are none yet, and, with "Lessons:SeedDemoData",
/// demo students and lessons for an empty timetable. Safe to run on every startup.
/// </summary>
public partial class LessonDataSeeder(
    ApplicationDbContext context,
    SchoolClock clock,
    IOptions<LessonSettings> options,
    ILogger<LessonDataSeeder> logger)
{
    /// <summary>
    /// Demo students get fake Telegram ids from here up: far above real user ids today,
    /// and reminders are off for them, so the bot never tries to message them.
    /// </summary>
    private const long DemoTelegramUserIdBase = 4_000_000_000_000_000;

    private const int DemoWeeksBack = 13;
    private const int DemoWeeksAhead = 2;

    private static readonly (string Name, string Color)[] DefaultTeachers =
    [
        ("Рома", "#22c55e"),
        ("Настя", "#64748b"),
        ("Аня", "#f59e0b"),
        ("Діана", "#d946ef")
    ];

    /// <summary>
    /// Start of the first and of the last lesson per weekday; days that are not listed are closed.
    /// </summary>
    private static readonly (string Room, (DayOfWeek Day, int First, int Last)[] Hours)[] DefaultRooms =
    [
        ("314", [(DayOfWeek.Monday, 10, 20), (DayOfWeek.Tuesday, 11, 20), (DayOfWeek.Wednesday, 11, 20), (DayOfWeek.Thursday, 10, 19), (DayOfWeek.Friday, 10, 20)]),
        ("305", [(DayOfWeek.Monday, 11, 20), (DayOfWeek.Tuesday, 11, 20), (DayOfWeek.Wednesday, 11, 20), (DayOfWeek.Thursday, 10, 19), (DayOfWeek.Friday, 11, 20)]),
        ("104", [(DayOfWeek.Monday, 11, 20), (DayOfWeek.Tuesday, 11, 20), (DayOfWeek.Wednesday, 11, 20), (DayOfWeek.Thursday, 11, 20), (DayOfWeek.Friday, 11, 20)])
    ];

    private static readonly (string FirstName, string? LastName)[] DemoStudents =
    [
        ("Діма", "Коваль"), ("Влад", "Мельник"), ("Арсен", "Бондар"), ("Святослав", "Ткачук"), ("Сашка", "Шевчук"),
        ("Мілана", "Кравець"), ("Вікторія", "Мороз"), ("Наталка", "Лисенко"), ("Нектарій", "Руденко"), ("Тіма", "Савчук"),
        ("Мілена", "Гончар"), ("Даша", "Романюк"), ("Лєна", "Петренко"), ("Анна", "Олійник"), ("Юля", "Кузьмич"),
        ("Віка", "Марченко"), ("Оля", "Поліщук"), ("Вікторія", "Кравчук"), ("Маргарита", "Остапенко"), ("Ксюша", "Литвин"),
        ("Олексій", "Семенко"), ("Софія", "Гриценко"), ("Василина", "Захарчук"), ("Нікіта", "Тимошенко"), ("Макс", "Білик"),
        ("Даша", "Антонюк"), ("Ангеліна", "Яковенко"), ("Емілія", "Довженко"), ("Іван", "Дмитренко"), ("Артем", "Сидоренко"),
        ("Аліса", "Юрченко")
    ];

    /// <summary>
    /// A week of the timetable, repeated over the demo weeks: day, room, start, student (index in <see cref="DemoStudents"/>
    /// or a note for a lesson without a student) and teacher (index in <see cref="DefaultTeachers"/>).
    /// </summary>
    private static readonly (DayOfWeek Day, string Room, string Time, int? Student, string? Note, int Teacher)[] DemoWeek =
    [
        (DayOfWeek.Monday, "314", "12:00", 15, null, 1), (DayOfWeek.Monday, "314", "13:00", 20, null, 1),
        (DayOfWeek.Monday, "314", "14:00", 17, null, 1), (DayOfWeek.Monday, "314", "15:00", 16, null, 1),
        (DayOfWeek.Monday, "314", "16:00", 5, null, 0), (DayOfWeek.Monday, "314", "18:00", 0, null, 0),
        (DayOfWeek.Monday, "314", "19:00", null, "Назар, онлайн", 0),
        (DayOfWeek.Monday, "305", "14:00", 4, null, 0), (DayOfWeek.Monday, "305", "15:00", 3, null, 0),
        (DayOfWeek.Monday, "305", "16:00", 28, null, 3), (DayOfWeek.Monday, "305", "17:00", 26, null, 2),
        (DayOfWeek.Monday, "104", "18:00", 23, null, 2),

        (DayOfWeek.Tuesday, "314", "12:00", 21, null, 1), (DayOfWeek.Tuesday, "314", "13:00", 14, null, 1),
        (DayOfWeek.Tuesday, "314", "15:00", 22, null, 1), (DayOfWeek.Tuesday, "314", "16:00", 11, null, 0),
        (DayOfWeek.Tuesday, "314", "17:00", 6, null, 0), (DayOfWeek.Tuesday, "314", "18:00", 0, null, 0),
        (DayOfWeek.Tuesday, "314", "19:00", 28, null, 3),
        (DayOfWeek.Tuesday, "305", "14:00", 2, null, 0), (DayOfWeek.Tuesday, "305", "15:00", 1, null, 0),
        (DayOfWeek.Tuesday, "305", "16:00", 30, null, 3), (DayOfWeek.Tuesday, "305", "17:00", 24, null, 2),
        (DayOfWeek.Tuesday, "305", "18:00", 27, null, 2),

        (DayOfWeek.Wednesday, "314", "11:00", 15, null, 1), (DayOfWeek.Wednesday, "314", "14:00", null, "Пробне, гітара", 0),
        (DayOfWeek.Wednesday, "314", "15:00", 20, null, 1), (DayOfWeek.Wednesday, "314", "16:00", 5, null, 0),
        (DayOfWeek.Wednesday, "314", "17:00", 14, null, 1), (DayOfWeek.Wednesday, "314", "18:00", 0, null, 0),
        (DayOfWeek.Wednesday, "305", "12:00", 7, null, 0), (DayOfWeek.Wednesday, "305", "15:00", 3, null, 0),
        (DayOfWeek.Wednesday, "305", "16:00", 29, null, 3),
        (DayOfWeek.Wednesday, "104", "17:00", 23, null, 2), (DayOfWeek.Wednesday, "104", "18:00", 29, null, 3),

        (DayOfWeek.Thursday, "314", "13:00", 17, null, 1), (DayOfWeek.Thursday, "314", "14:30", 16, null, 1),
        (DayOfWeek.Thursday, "314", "16:00", 28, null, 3), (DayOfWeek.Thursday, "314", "17:00", 8, null, 0),
        (DayOfWeek.Thursday, "314", "18:00", 10, null, 0),
        (DayOfWeek.Thursday, "305", "14:00", 4, null, 0), (DayOfWeek.Thursday, "305", "15:30", 9, null, 0),
        (DayOfWeek.Thursday, "305", "16:00", 13, null, 1),

        (DayOfWeek.Friday, "314", "14:00", 12, null, 1), (DayOfWeek.Friday, "314", "16:00", 18, null, 1),
        (DayOfWeek.Friday, "314", "17:00", 19, null, 1),
        (DayOfWeek.Friday, "305", "15:00", 2, null, 0), (DayOfWeek.Friday, "305", "17:00", 1, null, 0),
        (DayOfWeek.Friday, "104", "15:00", 23, null, 2), (DayOfWeek.Friday, "104", "16:00", 25, null, 2),
        (DayOfWeek.Friday, "104", "17:00", 24, null, 2), (DayOfWeek.Friday, "104", "18:00", 30, null, 3)
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedTeachersAsync(cancellationToken);
        await SeedRoomsAsync(cancellationToken);

        if (options.Value.SeedDemoData && !await context.Lessons.AnyAsync(cancellationToken))
            await SeedDemoLessonsAsync(cancellationToken);
    }

    private async Task SeedTeachersAsync(CancellationToken cancellationToken)
    {
        if (await context.Teachers.AnyAsync(cancellationToken))
            return;

        context.Teachers.AddRange(DefaultTeachers.Select((teacher, index) => new Teacher
        {
            Id = Guid.CreateVersion7(),
            Name = teacher.Name,
            Color = teacher.Color,
            SortOrder = index
        }));

        await context.SaveChangesAsync(cancellationToken);
        LogSeeded(logger, DefaultTeachers.Length, "teachers");
    }

    private async Task SeedRoomsAsync(CancellationToken cancellationToken)
    {
        if (await context.Rooms.AnyAsync(cancellationToken))
            return;

        context.Rooms.AddRange(DefaultRooms.Select((room, index) =>
        {
            var id = Guid.CreateVersion7();
            return new Room
            {
                Id = id,
                Name = room.Room,
                SortOrder = index,
                Hours = room.Hours
                    .Select(hours => new RoomHours
                    {
                        RoomId = id,
                        DayOfWeek = hours.Day,
                        FirstLessonAt = new TimeOnly(hours.First, 0),
                        LastLessonAt = new TimeOnly(hours.Last, 0)
                    })
                    .ToList()
            };
        }));

        await context.SaveChangesAsync(cancellationToken);
        LogSeeded(logger, DefaultRooms.Length, "rooms");
    }

    private async Task SeedDemoLessonsAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var today = clock.Today;

        var students = await EnsureDemoStudentsAsync(now, cancellationToken);
        var rooms = await context.Rooms.ToDictionaryAsync(room => room.Name, room => room.Id, cancellationToken);
        var teachers = await context.Teachers.OrderBy(teacher => teacher.SortOrder).Select(teacher => teacher.Id).ToListAsync(cancellationToken);

        // fixed seed: the same demo data on every machine
        var random = new Random(2026);
        var firstWeek = SchoolClock.StartOfWeek(today).AddDays(-7 * DemoWeeksBack);
        var lessons = new List<Lesson>();
        // each slot of the demo week is a weekly series
        var seriesIds = DemoWeek.Select(_ => Guid.CreateVersion7()).ToList();

        for (var week = 0; week <= DemoWeeksBack + DemoWeeksAhead; week++)
        {
            var monday = firstWeek.AddDays(7 * week);

            foreach (var (slot, seriesId) in DemoWeek.Zip(seriesIds))
            {
                var date = monday.AddDays(((int)slot.Day + 6) % 7);
                var isPast = date < today;

                // now and then a lesson did not happen, so the weeks are not all the same
                if (isPast && random.NextDouble() < 0.08)
                    continue;

                // most past lessons are paid, a few future ones are paid in advance
                var isPaid = random.NextDouble() < (isPast ? 0.9 : 0.15);

                lessons.Add(new Lesson
                {
                    Id = Guid.CreateVersion7(),
                    Date = date,
                    StartTime = TimeOnly.ParseExact(slot.Time, "HH:mm", CultureInfo.InvariantCulture),
                    RoomId = rooms[slot.Room],
                    TeacherId = teachers[slot.Teacher],
                    StudentId = slot.Student is { } student ? students[student] : null,
                    Note = slot.Note,
                    SeriesId = seriesId,
                    Price = options.Value.DefaultLessonPrice,
                    IsPaid = isPaid,
                    PaidAtUtc = isPaid ? now : null,
                    CreatedAtUtc = now
                });
            }
        }

        context.Lessons.AddRange(lessons);
        await context.SaveChangesAsync(cancellationToken);
        LogSeeded(logger, lessons.Count, "demo lessons");
    }

    /// <returns>Subscriber ids in the order of <see cref="DemoStudents"/>.</returns>
    private async Task<List<Guid>> EnsureDemoStudentsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var existing = await context.TelegramSubscribers
            .Where(subscriber => subscriber.TelegramUserId >= DemoTelegramUserIdBase)
            .ToDictionaryAsync(subscriber => subscriber.TelegramUserId, subscriber => subscriber.Id, cancellationToken);

        var ids = new List<Guid>(DemoStudents.Length);

        for (var index = 0; index < DemoStudents.Length; index++)
        {
            var telegramUserId = DemoTelegramUserIdBase + index;
            if (existing.TryGetValue(telegramUserId, out var id))
            {
                ids.Add(id);
                continue;
            }

            var student = new TelegramSubscriber
            {
                Id = Guid.CreateVersion7(),
                TelegramUserId = telegramUserId,
                ChatId = telegramUserId,
                FirstName = DemoStudents[index].FirstName,
                LastName = DemoStudents[index].LastName,
                LanguageCode = "uk",
                IsActive = true,
                // fake chats: sending would fail with "chat not found" and unsubscribe them
                RemindersEnabled = false,
                SubscribedAtUtc = now.AddDays(-7 * DemoWeeksBack)
            };

            context.TelegramSubscribers.Add(student);
            ids.Add(student.Id);
        }

        return ids;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded {Count} {What}")]
    private static partial void LogSeeded(ILogger logger, int count, string what);
}
