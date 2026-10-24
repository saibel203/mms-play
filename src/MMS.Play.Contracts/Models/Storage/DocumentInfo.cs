namespace MMS.Play.Contracts.Models.Storage;

/// <param name="Path">Path relative to the storage root, '/' separated.</param>
/// <param name="Size">Size in bytes, null when the provider does not report it.</param>
/// <param name="LastModified">Null when the provider does not report it.</param>
public record DocumentInfo(string Path, long? Size, DateTimeOffset? LastModified);
