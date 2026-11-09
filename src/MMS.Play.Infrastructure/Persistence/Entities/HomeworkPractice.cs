namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// The student marked the homework practised on <see cref="Date"/> (school date), in the bot or on the practice page.
/// One row per homework and day.
/// </summary>
public class HomeworkPractice
{
    public Guid HomeworkId { get; set; }
    public Homework Homework { get; set; } = null!;

    public DateOnly Date { get; set; }

    public DateTime PracticedAtUtc { get; set; }
}
