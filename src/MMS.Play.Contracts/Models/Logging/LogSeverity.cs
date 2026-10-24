namespace MMS.Play.Contracts.Models.Logging;

/// <summary>
/// Serilog levels from the least to the most severe. The names are the values of the Level column.
/// </summary>
public enum LogSeverity
{
    Verbose,
    Debug,
    Information,
    Warning,
    Error,
    Fatal
}
