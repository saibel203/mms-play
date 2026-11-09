using System.Text;
using Microsoft.EntityFrameworkCore;
using MMS.Play.Contracts.Models.Homework;
using MMS.Play.Infrastructure.Homeworks;
using MMS.Play.Infrastructure.Lessons;
using MMS.Play.Infrastructure.Persistence;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// /homework and «📚 Домашнє завдання»: the active homework with what to do, the days in a row and the practice page;
/// a student sees their own, a parent those of each linked child. The «✅ Позаймався» buttons (here and under the
/// reminders, callback "hw:done:&lt;id&gt;") mark a homework practised today.
/// </summary>
public class HomeworkBotHandler(ApplicationDbContext context, PracticeLinks practiceLinks, SchoolClock clock)
{
    // a chat message has room for plenty, but a long list is no longer readable; the rest is on the practice page
    private const int MaxListed = 10;

    public async Task HandleCommandAsync(ITelegramBotClient botClient, long chatId, long telegramUserId, CancellationToken cancellationToken)
    {
        var user = await context.TelegramSubscribers
            .Include(x => x.Children)
            .FirstOrDefaultAsync(x => x.TelegramUserId == telegramUserId, cancellationToken);

        if (user is null)
        {
            await botClient.SendMessage(chatId, "Спершу надішліть /start, щоб бот вас знав.", cancellationToken: cancellationToken);
            return;
        }

        if (user.IsParent && user.Children.Count == 0)
        {
            await botClient.SendMessage(
                chatId, "До вашого профілю ще не прив'язано дітей. Зверніться до адміністратора школи.", cancellationToken: cancellationToken);
            return;
        }

        List<Guid> studentIds = user.IsParent ? user.Children.Select(child => child.Id).ToList() : [user.Id];
        var rows = await HomeworkRow.ListAsync(
            context.Homeworks
                .Where(x => studentIds.Contains(x.StudentId) && x.Status == HomeworkStatus.Active)
                .OrderBy(x => x.Student.FirstName)
                .ThenByDescending(x => x.CreatedAtUtc),
            cancellationToken);

        if (rows.Count == 0)
        {
            var none = user.IsParent
                ? "📚 Зараз у ваших дітей немає домашніх завдань."
                : "📚 Зараз домашніх завдань немає. Викладач додасть їх після уроку 🙂";
            await botClient.SendMessage(chatId, none, cancellationToken: cancellationToken);
            return;
        }

        var today = clock.Today;
        var listed = rows.Take(MaxListed).ToList();
        var text = new StringBuilder("📚 <b>Домашнє завдання</b>");
        string? currentStudent = null;

        foreach (var row in listed)
        {
            if (user.IsParent && row.StudentName != currentStudent)
            {
                currentStudent = row.StudentName;
                text.Append($"\n\n👤 <b>{System.Net.WebUtility.HtmlEncode(currentStudent)}</b>");
            }

            text.Append("\n\n").Append(HomeworkTexts.Describe(row));

            var streak = row.Streak(today);
            if (row.PracticedOn(today))
                text.Append($"\n✅ Сьогодні вже позаймався · 🔥 {HomeworkTexts.Days(streak)} поспіль");
            else if (streak > 0)
                text.Append($"\n🔥 {HomeworkTexts.Days(streak)} поспіль — не перервіть серію!");
        }

        if (rows.Count > listed.Count)
            text.Append($"\n\n…і ще {rows.Count - listed.Count} на сторінці занять");

        var link = await practiceLinks.ForAsync(user, cancellationToken);
        if (HomeworkTexts.PracticeLinkHtml(link, practiceLinks.CanUseLinkButtons) is { Length: > 0 } linkHtml)
            text.Append("\n\n").Append(linkHtml);

        var keyboard = HomeworkTexts.Keyboard(
            listed.Where(row => !row.PracticedOn(today)).ToList(), link, practiceLinks.CanUseLinkButtons);

        await botClient.SendMessage(chatId, text.ToString(), ParseMode.Html, replyMarkup: keyboard, cancellationToken: cancellationToken);
    }

    public async Task HandleCallbackAsync(ITelegramBotClient botClient, CallbackQuery query, CancellationToken cancellationToken)
    {
        string? answer = null;
        try
        {
            if (query.Data is { } data && HomeworkTexts.ParseDoneCallback(data) is { } homeworkId)
                answer = await MarkPracticedAsync(botClient, query, data, homeworkId, cancellationToken);
        }
        finally
        {
            // always answered, otherwise the button keeps spinning on the user's phone
            await botClient.AnswerCallbackQuery(query.Id, answer, cancellationToken: cancellationToken);
        }
    }

    /// <returns>The short note Telegram shows on top of the chat.</returns>
    private async Task<string> MarkPracticedAsync(
        ITelegramBotClient botClient, CallbackQuery query, string data, Guid homeworkId, CancellationToken cancellationToken)
    {
        // the student, or a parent pressing it for their child
        var homework = await context.Homeworks
            .AsNoTracking()
            .Where(x => x.Id == homeworkId
                        && (x.Student.TelegramUserId == query.From.Id
                            || x.Student.Parents.Any(parent => parent.IsParent && parent.TelegramUserId == query.From.Id)))
            .Select(x => new { x.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (homework is null)
            return "Цього завдання вже немає.";
        if (homework.Status != HomeworkStatus.Active)
            return "Це завдання вже виконано 🎉";

        var today = clock.Today;
        var added = await HomeworkStats.MarkPracticedAsync(context, homeworkId, today, clock.UtcNow, cancellationToken);
        var days = await context.HomeworkPractices.Where(x => x.HomeworkId == homeworkId).Select(x => x.Date).ToListAsync(cancellationToken);
        var streak = HomeworkStats.Streak(days, today);

        if (query.Message is { } message)
            await RemoveButtonAsync(botClient, message, data, cancellationToken);

        return added
            ? $"✅ Записано! 🔥 {HomeworkTexts.Days(streak)} поспіль. Так тримати!"
            : "Сьогодні вже відмічено 👍";
    }

    /// <summary>
    /// The pressed «Позаймався» button goes, the others and the link stay.
    /// </summary>
    private static async Task RemoveButtonAsync(ITelegramBotClient botClient, Message message, string data, CancellationToken cancellationToken)
    {
        if (message.ReplyMarkup is not { } markup)
            return;

        var rows = markup.InlineKeyboard
            .Select(row => row.Where(button => button.CallbackData != data).ToArray())
            .Where(row => row.Length > 0)
            .ToArray();

        try
        {
            await botClient.EditMessageReplyMarkup(
                message.Chat.Id,
                message.Id,
                rows.Length == 0 ? null : new InlineKeyboardMarkup(rows),
                cancellationToken: cancellationToken);
        }
        catch (ApiRequestException)
        {
            // pressed twice, or the message is gone (/clear)
        }
    }
}
