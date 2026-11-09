using MMS.Play.Contracts.Models.Lessons;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Lessons;

public interface ILessonStatsService
{
    /// <summary>
    /// Lessons and earnings in the day, week, month, quarter and year containing <paramref name="date"/>, today when null.
    /// A lesson counts in the period of its date; earned means marked as paid.
    /// </summary>
    Task<LessonStatsDto> GetAsync(DateOnly? date, CancellationToken cancellationToken = default);
}
