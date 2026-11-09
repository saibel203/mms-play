namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// A room lessons take place in, open on the days listed in <see cref="Hours"/>.
/// </summary>
public class Room
{
    /// <summary>
    /// Version 7 GUID: time ordered, so inserts do not fragment the clustered index.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Room number, e.g. "314".
    /// </summary>
    public required string Name { get; set; }

    public int SortOrder { get; set; }

    public List<RoomHours> Hours { get; set; } = [];
}
