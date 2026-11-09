namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// Teaches lessons; the timetable shows a teacher's lessons in <see cref="Color"/>.
/// </summary>
public class Teacher
{
    /// <summary>
    /// Version 7 GUID: time ordered, so inserts do not fragment the clustered index.
    /// </summary>
    public Guid Id { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// CSS hex color, e.g. "#22c55e".
    /// </summary>
    public required string Color { get; set; }

    public int SortOrder { get; set; }
}
