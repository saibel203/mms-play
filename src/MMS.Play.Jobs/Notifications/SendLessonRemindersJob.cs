using Hangfire;
using Microsoft.Extensions.Logging;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;

namespace MMS.Play.Jobs.Notifications;

/// <summary>
/// Checks on the "Jobs:LessonReminders" schedule (every minute) whether a reminder time has come and, if so,
/// reminds the students and parents of the upcoming lessons; at the payment reminder time it also reminds those who
/// owe for lessons that took place, at the homework reminder time those with homework to practise, and at the
/// digest time it sends admins the daily summary. The times are in the notification settings.
/// </summary>
public partial class SendLessonRemindersJob(
    ILessonReminderService lessonReminderService,
    IPaymentReminderService paymentReminderService,
    IHomeworkReminderService homeworkReminderService,
    IAdminDigestService adminDigestService,
    ILogger<SendLessonRemindersJob> logger)
{
    public const string JobId = "send-lesson-reminders";

    // the next scheduled run picks up what failed, a retry would only add load
    [AutomaticRetry(Attempts = 0)]
    // two runs would race for the same reminders; the delivery rows stop duplicates, this stops the wasted work
    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await lessonReminderService.SendRemindersAsync(cancellationToken);

        // most runs fall between reminder times and do nothing, they would only flood the log
        if (result.Sent + result.Failed + result.Unsubscribed > 0)
            LogRemindersSent(logger, result.Sent, result.Failed, result.Unsubscribed);

        // each is logged by its service when it sends anything; one failing must not stop the others
        await RunAsync("payment reminders", () => paymentReminderService.SendDueAsync(cancellationToken));
        await RunAsync("homework reminders", () => homeworkReminderService.SendDueAsync(cancellationToken));
        await RunAsync("admin digest", () => adminDigestService.SendDueAsync(cancellationToken));
    }

    private async Task RunAsync(string what, Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSendFailed(logger, ex, what);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Lesson reminders: {Sent} sent, {Failed} failed, {Unsubscribed} unsubscribed")]
    private static partial void LogRemindersSent(ILogger logger, int sent, int failed, int unsubscribed);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sending {What} failed")]
    private static partial void LogSendFailed(ILogger logger, Exception exception, string what);
}
