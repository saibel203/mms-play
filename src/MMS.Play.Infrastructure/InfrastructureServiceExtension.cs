using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Logging;
using MMS.Play.Infrastructure.BackgroundJobs;
using MMS.Play.Infrastructure.HealthChecks;
using MMS.Play.Infrastructure.Logging;
using MMS.Play.Infrastructure.Metrics;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Serilog;

namespace MMS.Play.Infrastructure;

public static class InfrastructureServiceExtension
{
    private const string ConnectionStringName = "DefaultConnection";
    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(5);

    public static IServiceCollection AddInfrastructureService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilog((serviceProvider, loggerConfiguration) => loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .ReadFrom.Services(serviceProvider));

        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<DatabaseInitializer>();

        services.AddOptions<LogCleanupSettings>()
            .Bind(configuration.GetSection(LogCleanupSettings.SectionName))
            .Configure(settings => settings.LogFilePath = configuration[LogCleanupSettings.SerilogFilePathKey])
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<ILogCleanupService, LogCleanupService>();

        services.AddBackgroundJobs(configuration, connectionString);

        services.AddHealthChecks()
            // the API cannot serve requests without the database
            .AddDbContextCheck<ApplicationDbContext>("database", HealthStatus.Unhealthy)
            // without workers jobs pile up, but the API itself keeps working
            .AddCheck<HangfireHealthCheck>("hangfire", HealthStatus.Degraded);

        // the connection string allows a 900s connect timeout, a probe must fail long before that
        services.PostConfigure<HealthCheckServiceOptions>(options =>
        {
            foreach (var registration in options.Registrations)
            {
                registration.Timeout = HealthCheckTimeout;

                // the timeout only cancels a token, the wrapper makes sure nobody keeps waiting for a check that ignores it
                var factory = registration.Factory;
                registration.Factory = serviceProvider => new TimeoutHealthCheck(factory(serviceProvider));
            }
        });

        services.AddOpenTelemetryMetrics(configuration);

        return services;
    }

    /// <summary>
    /// Maps the Hangfire dashboard protected by Basic auth. Call after UseRouting.
    /// </summary>
    public static IApplicationBuilder UseJobsDashboard(this IApplicationBuilder app)
    {
        var settings = app.ApplicationServices.GetRequiredService<IOptions<HangfireSettings>>().Value.Dashboard;
        if (!settings.Enabled)
            return app;

        return app.UseHangfireDashboard(settings.Path, new DashboardOptions
        {
            DashboardTitle = settings.Title,
            DisplayStorageConnectionString = false,
            IsReadOnlyFunc = _ => settings.ReadOnly,
            Authorization = [new BasicAuthDashboardAuthorizationFilter(settings.Username, settings.Password)]
        });
    }

    private static void AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration, string connectionString)
    {
        services.AddOptions<HangfireSettings>()
            .Bind(configuration.GetSection(HangfireSettings.SectionName))
            .ValidateDataAnnotations()
            .Validate(settings => settings.Dashboard.Path.StartsWith('/'), "Hangfire:Dashboard:Path must start with '/'")
            .ValidateOnStart();

        // Hangfire creates and upgrades its own tables in a separate schema, they are not part of EF migrations
        services.AddHangfire((serviceProvider, config) => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                SchemaName = serviceProvider.GetRequiredService<IOptions<HangfireSettings>>().Value.SchemaName,
                PrepareSchemaIfNecessary = true,
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            }));

        services.AddHangfireServer((serviceProvider, options) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<HangfireSettings>>().Value;
            options.WorkerCount = settings.WorkerCount;
            options.Queues = settings.Queues;
        });
    }

    private static void AddOpenTelemetryMetrics(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(MetricsSettings.SectionName);

        services.AddOptions<MetricsSettings>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // decides what gets registered and feeds the OpenTelemetry builder, so it is read before the container is built
        var settings = section.Get<MetricsSettings>() ?? new MetricsSettings();
        if (!settings.Enabled)
            return;

        services.AddSingleton<IHealthCheckPublisher, HealthCheckMetricsPublisher>();

        services.AddOpenTelemetry()
            // a fixed instance id keeps the same series across restarts instead of a new random one each time
            .ConfigureResource(resource => resource.AddService(
                settings.ServiceName,
                autoGenerateServiceInstanceId: false,
                serviceInstanceId: Environment.MachineName))
            .WithMetrics(metrics => metrics
                // .NET emits these meters itself, no instrumentation packages are needed
                .AddMeter(
                    "Microsoft.AspNetCore.Hosting",     // http.server.request.duration: rate, latency, status codes per route
                    "Microsoft.AspNetCore.Diagnostics", // aspnetcore.diagnostics.exceptions: exceptions caught by UseExceptionHandler
                    "System.Runtime",                   // memory, GC, CPU, thread pool
                    HealthCheckMetricsPublisher.MeterName)
                // pushed over OTLP, so Prometheus does not need to reach the API
                .AddOtlpExporter((exporter, reader) =>
                {
                    exporter.Endpoint = new Uri(settings.OtlpEndpoint);
                    exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
                    reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = settings.ExportIntervalSeconds * 1000;
                }));
    }

    /// <summary>
    /// Creates the database if it does not exist and applies pending migrations.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();

        await initializer.InitializeDatabaseAsync();
    }

    /// <summary>
    /// Minimal logger used only until the host is built, so startup errors are not lost.
    /// </summary>
    public static void CreateBootstrapLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateBootstrapLogger();
    }
}
