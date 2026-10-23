namespace MMS.Play.Contracts.Models.Logging;

public record LogCleanupResult(DateTime CutoffUtc, int DeletedRows, int DeletedFiles);
