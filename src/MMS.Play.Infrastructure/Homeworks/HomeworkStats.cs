using Microsoft.EntityFrameworkCore;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Homeworks;

/// <summary>
/// Practice marks: «I practised today» from the bot and the practice page, and the days-in-a-row count.
/// </summary>
public static class HomeworkStats
{
    /// <summary>
    /// Days in a row with a mark, ending today, or yesterday while today is not marked yet (the day is not over).
    /// </summary>
    public static int Streak(IEnumerable<DateOnly> dates, DateOnly today)
    {
        var marked = dates as IReadOnlySet<DateOnly> ?? dates.ToHashSet();
        var day = marked.Contains(today) ? today : today.AddDays(-1);

        var count = 0;
        while (marked.Contains(day))
        {
            count++;
            day = day.AddDays(-1);
        }

        return count;
    }

    /// <summary>
    /// Marks the homework practised on <paramref name="today"/>.
    /// </summary>
    /// <returns>False when it was marked already.</returns>
    public static async Task<bool> MarkPracticedAsync(
        ApplicationDbContext context,
        Guid homeworkId,
        DateOnly today,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (await context.HomeworkPractices.AnyAsync(x => x.HomeworkId == homeworkId && x.Date == today, cancellationToken))
            return false;

        var practice = new HomeworkPractice { HomeworkId = homeworkId, Date = today, PracticedAtUtc = utcNow };
        context.HomeworkPractices.Add(practice);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // pressed in the bot and on the page at the same moment: the other one stored it
            context.Entry(practice).State = EntityState.Detached;
            return false;
        }
    }
}
