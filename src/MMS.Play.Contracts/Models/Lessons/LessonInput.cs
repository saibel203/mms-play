namespace MMS.Play.Contracts.Models.Lessons;

/// <summary>
/// What a lesson is made of, for creating and editing one. See <see cref="LessonDto"/> for the meaning of the fields.
/// A lesson needs a student, a note, or both.
/// </summary>
public record LessonInput(
    DateOnly Date,
    TimeOnly StartTime,
    Guid RoomId,
    Guid TeacherId,
    Guid? StudentId,
    string? Note,
    decimal Price,
    bool IsPaid);
