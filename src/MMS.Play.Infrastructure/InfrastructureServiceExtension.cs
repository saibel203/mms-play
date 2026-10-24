using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FluentStorage;
using FluentStorage.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MMS.Play.Contracts.Authorization;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Identity;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Logging;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Scores;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Storage;
using MMS.Play.Infrastructure.BackgroundJobs;
using MMS.Play.Infrastructure.HealthChecks;
using MMS.Play.Infrastructure.Identity;
using MMS.Play.Infrastructure.Logging;
using MMS.Play.Infrastructure.Metrics;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;
using MMS.Play.Infrastructure.Scores;
using MMS.Play.Infrastructure.Storage;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Serilog;

namespace MMS.Play.Infrastructure;

public static class InfrastructureServiceExtension
{
    private const string ConnectionStringName = "DefaultConnection";
    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(5);

    private const string AuthCookieName = "MMS.Play.Auth";
    private static readonly TimeSpan AuthCookieLifetime = TimeSpan.FromDays(14);

    public static IServiceCollection AddInfrastructureService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilog((serviceProvider, loggerConfiguration) => loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .ReadFrom.Services(serviceProvider));

        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<DatabaseInitializer>();

        services.AddIdentityService(configuration);

        services.AddOptions<LogCleanupSettings>()
            .Bind(configuration.GetSection(LogCleanupSettings.SectionName))
            .Configure(settings => settings.LogFilePath = configuration[LogCleanupSettings.SerilogFilePathKey])
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<ILogCleanupService, LogCleanupService>();
        services.AddScoped<ILogQueryService, LogQueryService>();

        services.AddDocumentStorage(configuration);
        services.AddScores(configuration);

        services.AddBackgroundJobs(configuration, connectionString);

        services.AddHealthChecks()
            // the API cannot serve requests without the database
            .AddDbContextCheck<ApplicationDbContext>("database", HealthStatus.Unhealthy)
            // without workers jobs pile up, but the API itself keeps working
            .AddCheck<HangfireHealthCheck>("hangfire", HealthStatus.Degraded)
            // scores cannot be uploaded or opened, but listing them still works
            .AddCheck<DocumentStorageHealthCheck>("document-storage", HealthStatus.Degraded);

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
    /// Maps the Hangfire dashboard, available to admins only. Call after UseAuthentication.
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
            Authorization = [new AdminDashboardAuthorizationFilter()]
        });
    }

    private static void AddIdentityService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IdentitySettings>()
            .Bind(configuration.GetSection(IdentitySettings.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                settings => settings.SeedUsers.All(user =>
                    !string.IsNullOrWhiteSpace(user.UserName)
                    && !string.IsNullOrWhiteSpace(user.Password)
                    && Roles.All.Contains(user.Role)),
                $"Identity:SeedUsers entries need UserName, Password and a Role out of: {string.Join(", ", Roles.All)}")
            .ValidateOnStart();

        // SignInManager.SignOutAsync signs out of the external and two-factor schemes too, so all Identity cookies are registered
        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.AddIdentityCore<User>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = AuthCookieName;
            // the client calls the API from the same site (dev proxy / reverse proxy), Strict keeps the cookie out of cross-site requests (CSRF)
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.HttpOnly = true;
            options.ExpireTimeSpan = AuthCookieLifetime;
            options.SlidingExpiration = true;

            // an API answers with status codes, the default handler would redirect to /Account/Login
            options.Events.OnRedirectToLogin = context => WriteStatusCode(context.Response, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => WriteStatusCode(context.Response, StatusCodes.Status403Forbidden);
        });

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IdentitySeeder>();
    }

    // the empty body is turned into problem details by UseStatusCodePages
    private static Task WriteStatusCode(HttpResponse response, int statusCode)
    {
        response.StatusCode = statusCode;
        return Task.CompletedTask;
    }

    private static void AddDocumentStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DocumentStorageSettings>()
            .Bind(configuration.GetSection(DocumentStorageSettings.SectionName))
            .Validate(settings => Enum.IsDefined(settings.Provider), "DocumentStorage:Provider is not supported")
            .Validate(
                settings => settings.Provider != DocumentStorageProvider.Local || !string.IsNullOrWhiteSpace(settings.Local.RootPath),
                "DocumentStorage:Local:RootPath is required for the Local provider")
            .ValidateOnStart();

        // a store holds the provider client and is thread safe, one instance serves the whole application
        services.AddSingleton(serviceProvider => CreateDocumentStore(
            serviceProvider.GetRequiredService<IOptions<DocumentStorageSettings>>().Value,
            serviceProvider.GetRequiredService<IHostEnvironment>()));

        services.AddSingleton<IDocumentManager, DocumentManager>();
    }

    private static IStore CreateDocumentStore(DocumentStorageSettings settings, IHostEnvironment environment)
    {
        switch (settings.Provider)
        {
            case DocumentStorageProvider.Local:
                var rootPath = Path.GetFullPath(settings.Local.RootPath, environment.ContentRootPath);
                Directory.CreateDirectory(rootPath);
                return StorageFactory.Disk(rootPath);

            default:
                throw new InvalidOperationException($"Document storage provider '{settings.Provider}' is not supported.");
        }
    }

    private static void AddScores(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ScoreSettings>()
            .Bind(configuration.GetSection(ScoreSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IScoreService, ScoreService>();
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
    /// Creates the roles and the "Identity:SeedUsers" accounts that do not exist yet. Requires a migrated database.
    /// </summary>
    public static async Task SeedIdentityAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();

        await seeder.SeedAsync();
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
