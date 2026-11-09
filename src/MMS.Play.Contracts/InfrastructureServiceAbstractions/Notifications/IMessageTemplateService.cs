using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.Models.Notifications;

namespace MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;

/// <summary>
/// The texts of the bot's messages: saved by an admin in the database, the appsettings defaults until then.
/// </summary>
public interface IMessageTemplateService
{
    /// <summary>
    /// Every kind, in the order of <see cref="MessageTemplateKind"/>.
    /// </summary>
    Task<IReadOnlyList<MessageTemplateDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<MessageTemplateDto> GetAsync(MessageTemplateKind kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the text, the next message of this kind uses it.
    /// </summary>
    /// <exception cref="ValidationFailedException">The text is empty, too long or is not valid Telegram HTML.</exception>
    Task<MessageTemplateDto> UpdateAsync(MessageTemplateKind kind, string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the saved text, the appsettings default is used again.
    /// </summary>
    Task<MessageTemplateDto> ResetAsync(MessageTemplateKind kind, CancellationToken cancellationToken = default);
}
