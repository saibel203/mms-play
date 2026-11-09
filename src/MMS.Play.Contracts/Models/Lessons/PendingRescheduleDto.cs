namespace MMS.Play.Contracts.Models.Lessons;

/// <summary>
/// A time the student proposed in the Telegram bot, waiting for an admin to approve or decline it.
/// </summary>
/// <param name="RequestId">Reschedule request identifier.</param>
/// <param name="Date">Proposed day.</param>
/// <param name="StartTime">Proposed start.</param>
/// <param name="RoomId">Room that was free at that time when the student proposed it.</param>
/// <param name="CreatedAt">When the student proposed it (UTC).</param>
public record PendingRescheduleDto(Guid RequestId, DateOnly Date, TimeOnly StartTime, Guid RoomId, DateTime CreatedAt);
