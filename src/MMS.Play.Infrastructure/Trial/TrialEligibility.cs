using Microsoft.EntityFrameworkCore;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;
using MMS.Play.Contracts.Models.Trial;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Telegram;
using Telegram.Bot.Types.ReplyMarkups;

namespace MMS.Play.Infrastructure.Trial;

/// <summary>
/// Who may ask for a trial lesson in the bot: newcomers only. Decided from the data every time the menu is sent,
/// so nothing has to be switched by hand: a subscriber stops being a newcomer as soon as they get a lesson, become
/// a parent or an admin, or send a request.
/// </summary>
public class TrialEligibility(ApplicationDbContext context, INotificationSettingsService settingsService)
{
    public enum Result
    {
        /// <summary>
        /// May ask.
        /// </summary>
        Eligible,

        /// <summary>
        /// Trial booking is turned off in the notification settings.
        /// </summary>
        Disabled,

        /// <summary>
        /// Not subscribed (never pressed Start, or stopped).
        /// </summary>
        NotSubscribed,

        /// <summary>
        /// Had a lesson as a student already, in any status.
        /// </summary>
        HasLessons,

        /// <summary>
        /// A parent with linked children: their children are students already.
        /// </summary>
        Parent,

        /// <summary>
        /// Admins do not book trial lessons for themselves.
        /// </summary>
        Admin,

        /// <summary>
        /// A request waits for an admin or was booked.
        /// </summary>
        AlreadyRequested
    }

    public async Task<Result> CheckAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var subscriber = await context.TelegramSubscribers
            .AsNoTracking()
            .Where(x => x.TelegramUserId == telegramUserId)
            .Select(x => new
            {
                x.Id,
                x.IsActive,
                x.IsAdmin,
                HasChildren = x.IsParent && x.Children.Any(),
                HasLessons = context.Lessons.Any(lesson => lesson.StudentId == x.Id),
                HasRequest = context.TrialRequests.Any(request =>
                    request.SubscriberId == x.Id
                    && (request.Status == TrialRequestStatus.New || request.Status == TrialRequestStatus.Booked))
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (subscriber is not { IsActive: true })
            return Result.NotSubscribed;
        if (subscriber.IsAdmin)
            return Result.Admin;
        if (subscriber.HasChildren)
            return Result.Parent;
        if (subscriber.HasLessons)
            return Result.HasLessons;
        if (subscriber.HasRequest)
            return Result.AlreadyRequested;

        // the settings last: the cheapest answer for most people is one query
        var settings = await settingsService.GetAsync(cancellationToken);
        return settings.TrialBookingEnabled ? Result.Eligible : Result.Disabled;
    }

    /// <summary>
    /// The menu under the message field for this user: with «🎸 Пробний урок» for a newcomer.
    /// </summary>
    public async Task<ReplyKeyboardMarkup> KeyboardAsync(long telegramUserId, CancellationToken cancellationToken) =>
        BotMenu.KeyboardFor(await CheckAsync(telegramUserId, cancellationToken) == Result.Eligible);

    /// <summary>
    /// Why the trial button is not there, for /trial typed by hand.
    /// </summary>
    public static string Explain(Result result) => result switch
    {
        Result.Disabled => "Запис на пробний урок зараз закритий. Напишіть адміністратору школи, і ми все домовимося.",
        Result.NotSubscribed => $"Спершу натисніть {BotMenu.StartCommand}, щоб підписатися на бота.",
        Result.HasLessons => "Пробний урок — для тих, хто ще не займався у нас. У вас уже є уроки: розклад — у «📅 Мій розклад».",
        Result.Parent => "Пробний урок — для нових учнів. Щоб записати ще одну дитину, напишіть адміністратору школи.",
        Result.Admin => "Адміністратори не записуються на пробний урок 🙂 Заявки приходять сюди та на сайт: Lessons → Trial requests.",
        Result.AlreadyRequested => "Вашу заявку на пробний урок уже отримано. Адміністратор зв'яжеться з вами найближчим часом.",
        _ => ""
    };
}
