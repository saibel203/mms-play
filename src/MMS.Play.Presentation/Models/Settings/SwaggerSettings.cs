using System.ComponentModel.DataAnnotations;

namespace MMS.Play.Presentation.Models.Settings;

/// <summary>
/// Bound from the "Swagger" section in appsettings.
/// </summary>
public class SwaggerSettings
{
    public const string SectionName = "Swagger";

    /// <summary>
    /// Exposes the OpenAPI documents and Swagger UI. Keep disabled outside of development/test environments.
    /// </summary>
    public bool Enabled { get; set; }

    [Required]
    public string Title { get; set; } = "MMS.Play API";

    /// <summary>
    /// Shared by every API version document, deprecated versions get a note appended.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Swagger UI path without leading slash, empty string serves the UI from the site root.
    /// </summary>
    public string RoutePrefix { get; set; } = "swagger";
}
