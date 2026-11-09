using Microsoft.Extensions.Options;
using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;
using MMS.Play.Contracts.Models.Notifications;
using MMS.Play.Infrastructure.Lessons;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Notifications;

/// <summary>
/// The bot's schedule and payment details, stored in the <see cref="SchoolSettings"/> row next to the lesson price.
/// </summary>
public class NotificationSettingsService(
    ApplicationDbContext context,
    SchoolSettingsDefaults defaults,
    IOptions<LessonSettings> lessonOptions) : INotificationSettingsService
{
    public async Task<NotificationSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var saved = await SchoolSettingsDefaults.FindAsync(context, track: false, cancellationToken);

        return saved is not null
            ? ToDto(saved, isDefault: false)
            : ToDto(defaults.Create(), isDefault: true);
    }

    public async Task<NotificationSettingsDto> UpdateAsync(NotificationSettingsUpdate update, CancellationToken cancellationToken = default)
    {
        var times = (update.ReminderTimes ?? []).Select(SchoolSettingsDefaults.TruncateToMinute).ToList();
        var paymentLink = Normalize(update.PaymentLink);
        var paymentDetails = Normalize(update.PaymentDetails);
        Validate(update, times, paymentLink, paymentDetails);

        // the lesson price keeps what it has (or gets its default on the first save)
        var settings = await defaults.GetOrAddAsync(context, cancellationToken);

        settings.RemindersEnabled = update.RemindersEnabled;
        settings.ReminderDaysBefore = update.ReminderDaysBefore;
        settings.ReminderTimes = times.Order().ToList();
        settings.ReminderIntervalMinutes = update.ReminderIntervalMinutes;
        settings.PaymentRemindersEnabled = update.PaymentRemindersEnabled;
        settings.PaymentReminderTime = SchoolSettingsDefaults.TruncateToMinute(update.PaymentReminderTime);
        settings.PaymentReminderEveryDays = update.PaymentReminderEveryDays;
        settings.PaymentLink = paymentLink;
        settings.PaymentDetails = paymentDetails;
        settings.HomeworkRemindersEnabled = update.HomeworkRemindersEnabled;
        settings.HomeworkReminderTime = SchoolSettingsDefaults.TruncateToMinute(update.HomeworkReminderTime);
        settings.AdminDigestEnabled = update.AdminDigestEnabled;
        settings.AdminDigestTime = SchoolSettingsDefaults.TruncateToMinute(update.AdminDigestTime);
        settings.TrialBookingEnabled = update.TrialBookingEnabled;
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return ToDto(settings, isDefault: false);
    }

    private NotificationSettingsDto ToDto(SchoolSettings settings, bool isDefault) => new(
        settings.RemindersEnabled,
        settings.ReminderDaysBefore,
        settings.ReminderTimes,
        settings.ReminderIntervalMinutes,
        settings.PaymentRemindersEnabled,
        settings.PaymentReminderTime,
        settings.PaymentReminderEveryDays,
        settings.PaymentLink,
        settings.PaymentDetails,
        settings.HomeworkRemindersEnabled,
        settings.HomeworkReminderTime,
        settings.AdminDigestEnabled,
        settings.AdminDigestTime,
        settings.TrialBookingEnabled,
        lessonOptions.Value.TimeZoneId,
        isDefault,
        isDefault ? null : settings.UpdatedAtUtc);

    /// <returns>The trimmed text, null when empty.</returns>
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Validate(NotificationSettingsUpdate update, List<TimeOnly> times, string? paymentLink, string? paymentDetails)
    {
        var errors = new Dictionary<string, string[]>();

        if (update.ReminderDaysBefore is < 0 or > NotificationSettingsDto.MaxReminderDaysBefore)
            errors[nameof(NotificationSettingsUpdate.ReminderDaysBefore)] =
                [$"Reminders start 0 to {NotificationSettingsDto.MaxReminderDaysBefore} days before a lesson."];

        if (times.Count is 0 or > NotificationSettingsDto.MaxReminderTimes)
            errors[nameof(NotificationSettingsUpdate.ReminderTimes)] = [$"Give 1 to {NotificationSettingsDto.MaxReminderTimes} reminder times."];
        else if (times.Distinct().Count() != times.Count)
            errors[nameof(NotificationSettingsUpdate.ReminderTimes)] = ["The reminder times repeat."];

        if (update.ReminderIntervalMinutes is < 1 or > NotificationSettingsDto.MaxReminderIntervalMinutes)
            errors[nameof(NotificationSettingsUpdate.ReminderIntervalMinutes)] =
                [$"The interval must be 1 to {NotificationSettingsDto.MaxReminderIntervalMinutes} minutes, or empty to use the times of day."];

        if (update.PaymentReminderEveryDays is < 1 or > NotificationSettingsDto.MaxPaymentReminderEveryDays)
            errors[nameof(NotificationSettingsUpdate.PaymentReminderEveryDays)] =
                [$"Remind about payment every 1 to {NotificationSettingsDto.MaxPaymentReminderEveryDays} days."];

        if (paymentLink is not null
            && (paymentLink.Length > NotificationSettingsDto.PaymentLinkMaxLength
                || !Uri.TryCreate(paymentLink, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)))
            errors[nameof(NotificationSettingsUpdate.PaymentLink)] =
                [$"The payment link must be a full http(s) address up to {NotificationSettingsDto.PaymentLinkMaxLength} characters, e.g. https://send.monobank.ua/jar/…"];

        if (paymentDetails?.Length > NotificationSettingsDto.PaymentDetailsMaxLength)
            errors[nameof(NotificationSettingsUpdate.PaymentDetails)] =
                [$"The payment details exceed {NotificationSettingsDto.PaymentDetailsMaxLength} characters."];

        if (errors.Count > 0)
            throw new ValidationFailedException(errors);
    }
}
