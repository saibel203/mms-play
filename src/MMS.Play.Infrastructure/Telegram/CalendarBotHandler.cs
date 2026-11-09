using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Trial;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// /calendar and «📆 Календар»: the personal calendar link (iCalendar) to subscribe to in Google or Apple Calendar,
/// and a button that replaces it when it leaked. The link carries <see cref="Persistence.Entities.TelegramSubscriber.AccessToken"/>,
/// which the practice page link shares, so replacing it changes both.
/// </summary>
public class CalendarBotHandler(
    ApplicationDbContext context,
    TrialEligibility eligibility,
    IOptions<PublicSiteSettings> publicSite)
{
    private const string Prefix = "cal:";
    /// <summary>cal:renew: asks whether to replace the link.</summary>
    private const string AskRenew = Prefix + "renew";
    /// <summary>cal:renew!: replaces it.</summary>
    private const string Renew = Prefix + "renew!";
    /// <summary>cal:keep: keeps it.</summary>
    private const string Keep = Prefix + "keep";

    public static bool IsCalendarCallback(string data) => data.StartsWith(Prefix, StringComparison.Ordinal);

    public async Task HandleCommandAsync(ITelegramBotClient botClient, long chatId, long telegramUserId, CancellationToken cancellationToken)
    {
        var (text, keyboard) = await BuildAsync(telegramUserId, renew: false, cancellationToken);
        await botClient.SendMessage(chatId, text, ParseMode.Html, replyMarkup: keyboard, linkPreviewOptions: true, cancellationToken: cancellationToken);
    }

    public async Task HandleCallbackAsync(ITelegramBotClient botClient, CallbackQuery query, CancellationToken cancellationToken)
    {
        try
        {
            if (query.Message is not { } message)
                return;

            switch (query.Data)
            {
                case AskRenew:
                    await EditAsync(botClient, message,
                        "🔄 <b>Замінити посилання?</b>\n\n"
                        + "Старе посилання перестане працювати — календар, підписаний на нього, більше не оновлюватиметься. "
                        + "Посилання на сторінку тренувань теж зміниться.",
                        new InlineKeyboardMarkup(new[]
                        {
                            InlineKeyboardButton.WithCallbackData("Так, замінити", Renew),
                            InlineKeyboardButton.WithCallbackData("Ні", Keep)
                        }),
                        cancellationToken);
                    break;

                case Renew:
                {
                    var (text, keyboard) = await BuildAsync(query.From.Id, renew: true, cancellationToken);
                    await EditAsync(botClient, message, "✅ Посилання замінено.\n\n" + text, keyboard as InlineKeyboardMarkup, cancellationToken);
                    break;
                }

                case Keep:
                {
                    var (text, keyboard) = await BuildAsync(query.From.Id, renew: false, cancellationToken);
                    await EditAsync(botClient, message, text, keyboard as InlineKeyboardMarkup, cancellationToken);
                    break;
                }
            }
        }
        finally
        {
            // always answered, otherwise the button keeps spinning on the user's phone
            await botClient.AnswerCallbackQuery(query.Id, cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// The message with the link and how to add it; the menu keyboard when there is nothing to press.
    /// </summary>
    private async Task<(string Text, ReplyMarkup Keyboard)> BuildAsync(long telegramUserId, bool renew, CancellationToken cancellationToken)
    {
        var subscriber = await context.TelegramSubscribers.FirstOrDefaultAsync(
            x => x.TelegramUserId == telegramUserId && x.IsActive, cancellationToken);
        if (subscriber is null)
            return ($"Спершу натисніть {BotMenu.StartCommand}, щоб підписатися на бота.", BotMenu.KeyboardFor(false));

        var site = publicSite.Value;
        if (!site.IsConfigured)
            return ("📆 Календар поки недоступний: адміністратор ще не налаштував адресу сайту.",
                await eligibility.KeyboardAsync(telegramUserId, cancellationToken));

        if (renew)
        {
            subscriber.AccessToken = TelegramSubscriberAccessToken.Create();
            await context.SaveChangesAsync(cancellationToken);
        }

        var token = await TelegramSubscriberAccessToken.EnsureAsync(context, subscriber, cancellationToken);
        var url = site.Url($"/api/v1/calendar/{token}.ics");
        var webcal = "webcal://" + url[(url.IndexOf("://", StringComparison.Ordinal) + 3)..];

        var text =
            "📆 <b>Уроки у вашому календарі</b>\n\n"
            + "Підпишіться на це посилання — уроки самі з'являться в календарі на телефоні й оновлюватимуться, "
            + "коли розклад зміниться:\n"
            + $"<code>{WebUtility.HtmlEncode(url)}</code>\n"
            + "(натисніть, щоб скопіювати)\n\n"
            + "<b>iPhone / Apple:</b> Параметри → Календар → Облікові записи → Додати обліковий запис → Інше → "
            + "«Додати підписний календар» і вставте посилання. Або відкрийте на iPhone:\n"
            + $"{WebUtility.HtmlEncode(webcal)}\n\n"
            + "<b>Google Календар:</b> на комп'ютері calendar.google.com → «Інші календарі» → «+» → «За URL-адресою» "
            + "і вставте посилання. На телефоні календар з'явиться сам за кілька хвилин.\n\n"
            + "🔒 Посилання особисте: не пересилайте його іншим.";

        return (text, new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("🔄 Нове посилання", AskRenew)));
    }

    private static async Task EditAsync(
        ITelegramBotClient botClient,
        Message message,
        string text,
        InlineKeyboardMarkup? keyboard,
        CancellationToken cancellationToken)
    {
        try
        {
            await botClient.EditMessageText(
                message.Chat.Id, message.Id, text, ParseMode.Html, replyMarkup: keyboard, linkPreviewOptions: true, cancellationToken: cancellationToken);
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
        {
            // the same button pressed twice
        }
    }
}
