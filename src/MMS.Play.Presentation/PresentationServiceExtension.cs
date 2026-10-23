using System.Text.Json.Serialization;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using MMS.Play.Presentation.Models.Settings;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MMS.Play.Presentation;

public static class PresentationServiceExtension
{
    // only our own assemblies that have GenerateDocumentationFile enabled produce these files
    private const string XmlDocumentationPattern = "MMS.Play.*.xml";

    public static IServiceCollection AddPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers()
            .AddApplicationPart(typeof(AssemblyReference).Assembly)
            // Swashbuckle picks up the converter too, so enums are documented by name
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // unhandled exceptions and empty error responses (404, 405...) are returned as RFC 9457 problem details
        services.AddProblemDetails();

        services.AddVersioning();
        services.AddSwagger(configuration);

        return services;
    }

    /// <summary>
    /// Serves an OpenAPI document per API version and Swagger UI when Swagger:Enabled is true.
    /// </summary>
    public static IApplicationBuilder UseApiDocumentation(this IApplicationBuilder app)
    {
        var settings = app.ApplicationServices.GetRequiredService<IOptions<SwaggerSettings>>().Value;
        if (!settings.Enabled)
            return app;

        var versionDescriptions = app.ApplicationServices.GetRequiredService<IApiVersionDescriptionProvider>().ApiVersionDescriptions;

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            // the first endpoint is selected by default, so the latest version goes first
            foreach (var description in versionDescriptions.OrderByDescending(description => description.ApiVersion))
                options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", $"{settings.Title} {description.GroupName}");

            options.RoutePrefix = settings.RoutePrefix;
            options.DocumentTitle = settings.Title;
            options.DisplayRequestDuration();
            options.EnableTryItOutByDefault();
        });

        return app;
    }

    private static void AddVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                // adds api-supported-versions / api-deprecated-versions response headers
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                // group name becomes the Swagger document name: v1, v2, v1.1
                options.GroupNameFormat = "'v'VVV";
                // documents /api/v1/test instead of /api/v{version}/test
                options.SubstituteApiVersionInUrl = true;
            });
    }

    private static void AddSwagger(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SwaggerSettings>()
            .Bind(configuration.GetSection(SwaggerSettings.SectionName))
            .ValidateDataAnnotations()
            .Validate(settings => !settings.RoutePrefix.StartsWith('/'), "Swagger:RoutePrefix must not start with '/'")
            .ValidateOnStart();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // SwaggerGenOptions are resolved lazily, so settings come from the validated IOptions instead of reading configuration here
        services.AddOptions<SwaggerGenOptions>()
            .Configure<IOptions<SwaggerSettings>, IApiVersionDescriptionProvider>((options, swaggerSettings, versionProvider) =>
            {
                var settings = swaggerSettings.Value;

                foreach (var description in versionProvider.ApiVersionDescriptions)
                {
                    options.SwaggerDoc(description.GroupName, new OpenApiInfo
                    {
                        Title = settings.Title,
                        Version = description.ApiVersion.ToString(),
                        Description = description.IsDeprecated
                            ? $"{settings.Description} This API version is deprecated."
                            : settings.Description
                    });
                }

                // xml files of referenced projects are copied to the host output folder, not next to this assembly's source
                foreach (var xmlFile in Directory.EnumerateFiles(AppContext.BaseDirectory, XmlDocumentationPattern))
                    options.IncludeXmlComments(xmlFile, includeControllerXmlComments: true);

                options.SupportNonNullableReferenceTypes();
            });
    }
}
