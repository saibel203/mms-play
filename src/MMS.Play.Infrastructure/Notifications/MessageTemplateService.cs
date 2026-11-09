using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;
using MMS.Play.Contracts.Models.Notifications;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;
using MMS.Play.Infrastructure.Telegram;

namespace MMS.Play.Infrastructure.Notifications;

/// <summary>
/// The <see cref="MessageTemplate"/> rows, keyed by the <see cref="MessageTemplateKind"/> name
/// ("LessonReminder" was the key before there were other kinds, so saved reminder texts are kept).
/// </summary>
public class MessageTemplateService(
    ApplicationDbContext context,
    IOptions<LessonReminderSettings> reminderOptions,
    IOptions<PaymentReminderSettings> paymentOptions,
    IOptions<HomeworkReminderSettings> homeworkOptions) : IMessageTemplateService
{
    private const string TextField = nameof(MessageTemplateDto.Text);

    public async Task<IReadOnlyList<MessageTemplateDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var saved = await context.MessageTemplates.AsNoTracking().ToListAsync(cancellationToken);

        return Enum.GetValues<MessageTemplateKind>()
            .Select(kind => saved.FirstOrDefault(x => x.Key == Key(kind)) is { } template
                ? ToDto(kind, template)
                : DefaultTemplate(kind))
            .ToList();
    }

    public async Task<MessageTemplateDto> GetAsync(MessageTemplateKind kind, CancellationToken cancellationToken = default)
    {
        var key = Key(kind);
        var template = await context.MessageTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, cancellationToken);

        return template is null ? DefaultTemplate(kind) : ToDto(kind, template);
    }

    public async Task<MessageTemplateDto> UpdateAsync(MessageTemplateKind kind, string text, CancellationToken cancellationToken = default)
    {
        // browsers send \r\n from a textarea, Telegram needs \n only
        text = (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        Validate(text);

        var key = Key(kind);
        var template = await context.MessageTemplates.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (template is null)
        {
            template = new MessageTemplate { Key = key, Text = text };
            context.MessageTemplates.Add(template);
        }

        template.Text = text;
        template.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return ToDto(kind, template);
    }

    public async Task<MessageTemplateDto> ResetAsync(MessageTemplateKind kind, CancellationToken cancellationToken = default)
    {
        var key = Key(kind);
        await context.MessageTemplates
            .Where(x => x.Key == key)
            .ExecuteDeleteAsync(cancellationToken);

        return DefaultTemplate(kind);
    }

    private static string Key(MessageTemplateKind kind) => kind.ToString();

    private static MessageTemplateDto ToDto(MessageTemplateKind kind, MessageTemplate template) =>
        new(kind, template.Text, IsDefault: false, template.UpdatedAtUtc);

    private MessageTemplateDto DefaultTemplate(MessageTemplateKind kind) => new(kind, DefaultText(kind), IsDefault: true, UpdatedAt: null);

    private string DefaultText(MessageTemplateKind kind) => kind switch
    {
        MessageTemplateKind.LessonReminder => reminderOptions.Value.MessageTemplate,
        MessageTemplateKind.ParentLessonReminder => reminderOptions.Value.ParentMessageTemplate,
        MessageTemplateKind.PaymentReminder => paymentOptions.Value.StudentMessageTemplate,
        MessageTemplateKind.ParentPaymentReminder => paymentOptions.Value.ParentMessageTemplate,
        MessageTemplateKind.HomeworkAssigned => homeworkOptions.Value.AssignedMessageTemplate,
        MessageTemplateKind.HomeworkReminder => homeworkOptions.Value.ReminderMessageTemplate,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static void Validate(string text)
    {
        if (text.Length == 0)
            throw new ValidationFailedException(TextField, "The text is required.");

        if (text.Length > MessageTemplateDto.TextMaxLength)
            throw new ValidationFailedException(TextField, $"The text exceeds {MessageTemplateDto.TextMaxLength} characters.");

        if (TelegramHtml.Validate(text) is { } error)
            throw new ValidationFailedException(TextField, error);
    }
}
