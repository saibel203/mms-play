using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.Models.Lessons;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Lessons;

/// <summary>
/// The teachers and rooms of the timetable. New ones go to the end of the list.
/// Only a teacher or room without lessons can be deleted: lessons and earnings keep referring to them.
/// </summary>
public interface ISchoolResourceService
{
    Task<IReadOnlyList<TeacherSummaryDto>> ListTeachersAsync(CancellationToken cancellationToken = default);

    /// <exception cref="ValidationFailedException">An empty or taken name, or not a "#rrggbb" color.</exception>
    Task<TeacherSummaryDto> CreateTeacherAsync(TeacherInput input, CancellationToken cancellationToken = default);

    /// <returns>Null when the teacher does not exist.</returns>
    /// <exception cref="ValidationFailedException">An empty or taken name, or not a "#rrggbb" color.</exception>
    Task<TeacherSummaryDto?> UpdateTeacherAsync(Guid id, TeacherInput input, CancellationToken cancellationToken = default);

    /// <returns>False when the teacher does not exist.</returns>
    /// <exception cref="ValidationFailedException">The teacher has lessons.</exception>
    Task<bool> DeleteTeacherAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoomSummaryDto>> ListRoomsAsync(CancellationToken cancellationToken = default);

    /// <exception cref="ValidationFailedException">An empty or taken name, or invalid hours.</exception>
    Task<RoomSummaryDto> CreateRoomAsync(RoomInput input, CancellationToken cancellationToken = default);

    /// <returns>Null when the room does not exist.</returns>
    /// <exception cref="ValidationFailedException">An empty or taken name, or invalid hours.</exception>
    Task<RoomSummaryDto?> UpdateRoomAsync(Guid id, RoomInput input, CancellationToken cancellationToken = default);

    /// <returns>False when the room does not exist.</returns>
    /// <exception cref="ValidationFailedException">The room has lessons or is proposed in a reschedule request.</exception>
    Task<bool> DeleteRoomAsync(Guid id, CancellationToken cancellationToken = default);
}
