using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.Models.Homework;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Homework;

/// <summary>
/// Homework an admin gives students: a score from the library (optional), bars, tempo and what to do.
/// The student is told in the bot and reminded every day to practise.
/// </summary>
public interface IHomeworkService
{
    /// <summary>
    /// Active first, then newest first.
    /// </summary>
    Task<IReadOnlyList<HomeworkDto>> ListAsync(HomeworkListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives homework; the student is told in the bot (a message that does not go out does not fail it).
    /// </summary>
    /// <exception cref="ValidationFailedException">Unknown or unsubscribed student, unknown score, no title, bars or tempo out of range.</exception>
    Task<HomeworkDto> CreateAsync(HomeworkInput input, CancellationToken cancellationToken = default);

    /// <returns>Null when the homework does not exist.</returns>
    /// <exception cref="ValidationFailedException">See <see cref="CreateAsync"/>.</exception>
    Task<HomeworkDto?> UpdateAsync(Guid id, HomeworkInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks it done (no more reminders) or active again.
    /// </summary>
    /// <returns>Null when the homework does not exist.</returns>
    Task<HomeworkDto?> SetStatusAsync(Guid id, HomeworkStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes it with its practice history; false when it does not exist.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
