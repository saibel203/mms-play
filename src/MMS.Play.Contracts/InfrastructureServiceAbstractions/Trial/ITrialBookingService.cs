using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.Models.Trial;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Trial;

/// <summary>
/// Trial lessons for newcomers: free times, requests from the public page and the bot, booking them as lessons.
/// </summary>
public interface ITrialBookingService
{
    /// <summary>
    /// Free times of the next days ("TrialBooking:DaysAhead"), from "TrialBooking:MinNoticeHours" from now.
    /// </summary>
    Task<TrialSlotsDto> GetSlotsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a request from the public page and tells the admins in the bot.
    /// </summary>
    /// <exception cref="ValidationFailedException">Booking is off, a field is invalid, the time is taken, or too many requests came lately.</exception>
    Task CreateFromWebsiteAsync(TrialRequestInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Newest first; all statuses when <paramref name="status"/> is null.
    /// </summary>
    Task<IReadOnlyList<TrialRequestDto>> ListAsync(TrialRequestStatus? status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the lesson and marks the request booked; a bot requester is told in the bot.
    /// </summary>
    /// <returns>Null when there is no such request.</returns>
    /// <exception cref="ValidationFailedException">Already decided, or the lesson is invalid or clashes.</exception>
    Task<TrialRequestDto?> BookAsync(Guid id, TrialBookingInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns the request down; a bot requester is told in the bot, with <paramref name="reason"/> when given.
    /// </summary>
    /// <returns>Null when there is no such request.</returns>
    /// <exception cref="ValidationFailedException">Already decided.</exception>
    Task<TrialRequestDto?> DeclineAsync(Guid id, string? reason, CancellationToken cancellationToken = default);
}
