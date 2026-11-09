using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Lessons;

/// <summary>
/// The <see cref="SchoolSettings"/> row as appsettings define it, used while nothing is saved. The lesson settings
/// and the notification settings share the row: whichever is saved first creates it with the other's defaults,
/// so saving one never wipes the other.
/// </summary>
public class SchoolSettingsDefaults(
    IOptions<LessonSettings> lessonOptions,
    IOptions<LessonReminderSettings> reminderOptions,
    IOptions<PaymentReminderSettings> paymentOptions,
    IOptions<HomeworkReminderSettings> homeworkOptions,
    IOptions<AdminDigestSettings> digestOptions,
    IOptions<TrialBookingSettings> trialOptions)
{
    public SchoolSettings Create()
    {
        var reminders = reminderOptions.Value;
        var payments = paymentOptions.Value;
        var homework = homeworkOptions.Value;
        var digest = digestOptions.Value;

        return new SchoolSettings
        {
            DefaultLessonPrice = lessonOptions.Value.DefaultLessonPrice,
            RemindersEnabled = reminders.Enabled,
            ReminderDaysBefore = reminders.DaysBefore,
            ReminderTimes = reminders.Times.Select(TruncateToMinute).Distinct().Order().ToList(),
            ReminderIntervalMinutes = reminders.IntervalMinutes,
            PaymentRemindersEnabled = payments.Enabled,
            PaymentReminderTime = TruncateToMinute(payments.Time),
            PaymentReminderEveryDays = payments.EveryDays,
            HomeworkRemindersEnabled = homework.Enabled,
            HomeworkReminderTime = TruncateToMinute(homework.Time),
            AdminDigestEnabled = digest.Enabled,
            AdminDigestTime = TruncateToMinute(digest.Time),
            TrialBookingEnabled = trialOptions.Value.Enabled
        };
    }

    /// <summary>
    /// The saved row, null while nothing is saved.
    /// </summary>
    public static Task<SchoolSettings?> FindAsync(ApplicationDbContext context, bool track, CancellationToken cancellationToken)
    {
        var query = track ? context.SchoolSettings : context.SchoolSettings.AsNoTracking();
        return query.FirstOrDefaultAsync(x => x.Id == SchoolSettings.SingletonId, cancellationToken);
    }

    /// <summary>
    /// The saved row, tracked; a new one filled with the defaults (added, not saved) when there is none.
    /// </summary>
    public async Task<SchoolSettings> GetOrAddAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        if (await FindAsync(context, track: true, cancellationToken) is { } saved)
            return saved;

        var settings = Create();
        context.SchoolSettings.Add(settings);
        return settings;
    }

    // seconds would make 09:00:30 and 09:00 two different reminders
    public static TimeOnly TruncateToMinute(TimeOnly time) => new(time.Hour, time.Minute);
}
