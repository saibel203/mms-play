using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Logging;
using MMS.Play.Infrastructure.BackgroundJobs;
using MMS.Play.Infrastructure.Logging;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence;
using Serilog;

namespace MMS.Play.Infrastructure;

public static class InfrastructureServiceExtension
{
    private const string ConnectionStringName = "DefaultConnection";

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
