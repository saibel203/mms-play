namespace MMS.Play.Contracts.Models.Lessons;

/// <summary>
/// Calendar periods around a date: the day itself, its week (Monday to Sunday), month, quarter and year.
/// </summary>
public enum StatsPeriod
{
    Day,
    Week,
    Month,
    Quarter,
    Year
}

/// <summary>
/// Earnings in every <see cref="StatsPeriod"/> containing <paramref name="Date"/>.
/// </summary>
public record LessonStatsDto(DateOnly Date, IReadOnlyList<PeriodStatsDto> Periods);

/// <param name="Period">Which period.</param>
/// <param name="From">First day, inclusive.</param>
/// <param name="To">Last day, inclusive.</param>
/// <param name="LessonCount">All lessons in the period.</param>
/// <param name="PaidCount">Lessons marked as paid.</param>
/// <param name="PaidAmount">Earned: the sum of the prices of the paid lessons.</param>
/// <param name="UnpaidAmount">Still expected: the sum of the prices of the lessons not paid yet.</param>
/// <param name="Teachers">The same numbers per teacher, every teacher listed.</param>
/// <param name="CancelledCount">Cancelled lessons (not in <paramref name="LessonCount"/>).</param>
/// <param name="LateCancelledCount">Of them, cancelled less than 24 hours before the start.</param>
/// <param name="CancelledByAdminCount">Of them, cancelled by an admin; the rest by students and parents in the bot.</param>
/// <param name="TopCancellers">The students who cancelled the most, at most 10.</param>
public record PeriodStatsDto(
    StatsPeriod Period,
    DateOnly From,
    DateOnly To,
    int LessonCount,
    int PaidCount,
    decimal PaidAmount,
    decimal UnpaidAmount,
    IReadOnlyList<TeacherStatsDto> Teachers,
    int CancelledCount,
    int LateCancelledCount,
    int CancelledByAdminCount,
    IReadOnlyList<StudentCancellationsDto> TopCancellers);

/// <param name="StudentId">Null for the lessons without a student.</param>
/// <param name="Name">Student name.</param>
/// <param name="CancelledCount">Cancelled lessons in the period.</param>
/// <param name="LateCount">Of them, cancelled late.</param>
public record StudentCancellationsDto(Guid? StudentId, string Name, int CancelledCount, int LateCount);

public record TeacherStatsDto(
    Guid TeacherId,
    string Name,
    string Color,
    int LessonCount,
    int PaidCount,
    decimal PaidAmount,
    decimal UnpaidAmount,
    int CancelledCount,
    int LateCancelledCount);
