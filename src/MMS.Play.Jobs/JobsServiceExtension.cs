using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Jobs.Logging;

namespace MMS.Play.Jobs;

public static class JobsServiceExtension
{
    public static IServiceCollection AddJobs(this IServiceCollection services)
    {
        services.AddScoped<CleanupLogsJob>();
        return services;
    }

    /// <summary>
    /// Creates or updates recurring jobs in Hangfire storage. Schedules are evaluated in UTC.
    /// </summary>
    public static IServiceProvider RegisterRecurringJobs(this IServiceProvider serviceProvider)
    {
        var recurringJobManager = serviceProvider.GetRequiredService<IRecurringJobManager>();
        var logCleanupSettings = serviceProvider.GetRequiredService<IOptions<LogCleanupSettings>>().Value;

        // Hangfire passes the server shutdown token in place of CancellationToken.None
        recurringJobManager.AddOrUpdate<CleanupLogsJob>(
            CleanupLogsJob.JobId,
            job => job.ExecuteAsync(CancellationToken.None),
            logCleanupSettings.Schedule);

        return serviceProvider;
    }
}
