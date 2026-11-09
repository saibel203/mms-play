using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MMS.Play.Jobs.Logging;
using MMS.Play.Jobs.Models.Settings;
using MMS.Play.Jobs.Notifications;

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

        var lessonRemindersSection = configuration.GetSection(SendLessonRemindersJobSettings.SectionName);

        services.AddOptions<SendLessonRemindersJobSettings>()
            .Bind(lessonRemindersSection)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // the job needs ILessonReminderService, which exists only while the Telegram bot is enabled.
        // Registered only when turned on: enabling the job without the bot then fails at startup (ValidateOnBuild in Development)
        if ((lessonRemindersSection.Get<SendLessonRemindersJobSettings>() ?? new SendLessonRemindersJobSettings()).Enabled)
            services.AddScoped<SendLessonRemindersJob>();

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

        var lessonRemindersSettings = serviceProvider.GetRequiredService<IOptions<SendLessonRemindersJobSettings>>().Value;
        if (lessonRemindersSettings.Enabled)
        {
            recurringJobManager.AddOrUpdate<SendLessonRemindersJob>(
                SendLessonRemindersJob.JobId,
                job => job.ExecuteAsync(CancellationToken.None),
                lessonRemindersSettings.Schedule);
        }
        else
        {
            // the job stays in Hangfire storage after it was turned off, it has to be removed explicitly
            recurringJobManager.RemoveIfExists(SendLessonRemindersJob.JobId);
        }

        return serviceProvider;
    }
}
