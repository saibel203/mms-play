using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;
using MMS.Play.Contracts.Models.Notifications;
using MMS.Play.Infrastructure.Homeworks;
using MMS.Play.Infrastructure.Lessons;
using MMS.Play.Infrastructure.Persistence;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// The «HomeworkAssigned» message to the student when an admin gives homework, with the «✅ Позаймався» and
/// practice page buttons. Sent whatever the "Reminders" checkbox says: it is news, not a reminder.
/// </summary>
public partial class TelegramHomeworkNotifier(
    ITelegramBotClient botClient,
    ApplicationDbContext context,
    IMessageTemplateService templateService,
    PracticeLinks practiceLinks,
    SchoolClock clock,
    ILogger<TelegramHomeworkNotifier> logger) : IHomeworkNotifier
{
    public async Task NotifyAssignedAsync(Guid homeworkId, CancellationToken cancellationToken)
    {
        try
        {
            var homework = await HomeworkRow.FindAsync(context, homeworkId, cancellationToken);
            if (homework is null)
                return;

            var student = await context.TelegramSubscribers
                .FirstOrDefaultAsync(x => x.Id == homework.StudentId && x.IsActive && !x.IsParent, cancellationToken);
            if (student is null)
                return;

            var link = await practiceLinks.ForAsync(student, cancellationToken);
            var template = (await templateService.GetAsync(MessageTemplateKind.HomeworkAssigned, cancellationToken)).Text;
            var text = HomeworkTexts.Render(
                template,
                student.FirstName,
                HomeworkTexts.Describe(homework, withDueDate: false),
                HomeworkTexts.DueDateLine(homework.DueDate),
                homework.Streak(clock.Today),
                HomeworkTexts.PracticeLinkHtml(link, practiceLinks.CanUseLinkButtons));

            await botClient.SendMessage(
                student.ChatId,
                text,
                ParseMode.Html,
                replyMarkup: HomeworkTexts.Keyboard([homework], link, practiceLinks.CanUseLinkButtons),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // blocked bot, network: the homework is stored and the student finds it with /homework
            LogNotifyFailed(logger, ex, homeworkId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not tell the student about homework {HomeworkId} in Telegram")]
    private static partial void LogNotifyFailed(ILogger logger, Exception exception, Guid homeworkId);
}
