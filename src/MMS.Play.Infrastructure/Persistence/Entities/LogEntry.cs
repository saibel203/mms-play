namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// Row written by the Serilog MSSqlServer sink. Columns must match "Serilog:WriteTo:MSSqlServer:Args:columnOptionsSection" in appsettings.
/// </summary>
public class LogEntry
{
    public int Id { get; set; }
    public string? Message { get; set; }
    public string? MessageTemplate { get; set; }
    public string? Level { get; set; }
    public DateTime TimeStamp { get; set; }
    public string? Exception { get; set; }
    public string? LogEvent { get; set; }
    public string? SourceContext { get; set; }
}
