using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.Models.Lessons;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Lessons;

/// <summary>
/// Who paid for their lessons and who did not.
/// </summary>
public interface ILessonPaymentService
{
    /// <param name="from">First day, null for no limit.</param>
    /// <param name="to">Last day, null for no limit.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <exception cref="ValidationFailedException"><paramref name="from"/> is after <paramref name="to"/>.</exception>
    Task<PaymentOverviewDto> GetOverviewAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks lessons as paid or unpaid at once, e.g. everything a student owes. Rescheduled lessons are skipped.
    /// </summary>
    /// <returns>How many lessons changed.</returns>
    /// <exception cref="ValidationFailedException">No lessons, or more than <see cref="MaxBulkLessons"/>.</exception>
    Task<int> SetPaidAsync(IReadOnlyList<Guid> lessonIds, bool isPaid, CancellationToken cancellationToken = default);

    public const int MaxBulkLessons = 500;
}
