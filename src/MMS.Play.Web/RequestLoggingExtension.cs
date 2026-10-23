using Microsoft.Extensions.Options;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Presentation.Models.Settings;
using Serilog;
using Serilog.Events;

namespace MMS.Play.Web;

public static class RequestLoggingExtension
{
    /// <summary>
    /// Serilog request logging that skips Swagger and Hangfire dashboard traffic unless the request failed.
    /// </summary>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        var quietPaths = GetQuietPaths(app.ApplicationServices);

        return app.UseSerilogRequestLogging(options => options.GetLevel = (httpContext, _, exception) =>
        {
            // same rule as Serilog's default level selection
            if (exception is not null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError)
                return LogEventLevel.Error;

            // below the configured minimum level, so these events are dropped
            return quietPaths.Any(path => httpContext.Request.Path.StartsWithSegments(path))
                ? LogEventLevel.Verbose
                : LogEventLevel.Information;
        });
    }

    private static PathString[] GetQuietPaths(IServiceProvider serviceProvider)
    {
        var swagger = serviceProvider.GetRequiredService<IOptions<SwaggerSettings>>().Value;
        var hangfire = serviceProvider.GetRequiredService<IOptions<HangfireSettings>>().Value.Dashboard;

        // swagger.json is always served from /swagger, the UI can be moved by RoutePrefix
        List<PathString> paths = ["/swagger"];

        // an empty prefix serves the UI from the root, which must not silence every request
        if (!string.IsNullOrEmpty(swagger.RoutePrefix))
            paths.Add($"/{swagger.RoutePrefix}");

        if (hangfire.Enabled)
            paths.Add(hangfire.Path);

        return [.. paths.Distinct()];
    }
}
