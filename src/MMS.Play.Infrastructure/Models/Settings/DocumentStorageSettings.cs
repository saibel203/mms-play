namespace MMS.Play.Infrastructure.Models.Settings;

/// <summary>
/// Bound from the "DocumentStorage" section in appsettings.
/// </summary>
public class DocumentStorageSettings
{
    public const string SectionName = "DocumentStorage";

    /// <summary>
    /// Selects where documents are stored, only the settings of the selected provider are used.
    /// </summary>
    public DocumentStorageProvider Provider { get; set; } = DocumentStorageProvider.Local;

    public LocalDocumentStorageSettings Local { get; set; } = new();
}

/// <summary>
/// A cloud provider is added as a new member plus a FluentStorage module (FluentStorage.AWS, FluentStorage.Azure.Blobs, FluentStorage.GCP)
/// and its case in InfrastructureServiceExtension.CreateDocumentStore.
/// </summary>
public enum DocumentStorageProvider
{
    Local
}

public class LocalDocumentStorageSettings
{
    /// <summary>
    /// Root folder of the storage. A relative path is resolved against the content root (the MMS.Play.Web project folder when run from the IDE).
    /// </summary>
    public string RootPath { get; set; } = "data";
}
