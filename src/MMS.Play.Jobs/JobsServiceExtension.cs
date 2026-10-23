using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MMS.Play.Jobs.Logging;
using MMS.Play.Jobs.Models.Settings;

namespace MMS.Play.Jobs;

public static class JobsServiceExtension
{
    public static IServiceCollection AddJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CleanupLogsJobSettings>()
            .Bind(configuration.GetSection(CleanupLogsJobSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<CleanupLogsJob>();

        return services;
    }

    /// <summary>
    /// Creates or updates recurring jobs in Hangfire storage. Schedules are evaluated in UTC.
    /// </summary>
    public static IServiceProvider RegisterRecurringJobs(this IServiceProvider serviceProvider)
    {
        var recurringJobManager = serviceProvider.GetRequiredService<IRecurringJobManager>();
        var cleanupLogsSettings = serviceProvider.GetRequiredService<IOptions<CleanupLogsJobSettings>>().Value;

        // Hangfire passes the server shutdown token in place of CancellationToken.None
        recurringJobManager.AddOrUpdate<CleanupLogsJob>(
            CleanupLogsJob.JobId,
            job => job.ExecuteAsync(CancellationToken.None),
            cleanupLogsSettings.Schedule);

        return serviceProvider;
    }
}
