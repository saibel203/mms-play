namespace MMS.Play.Contracts.Models.Notifications;

/// <summary>
/// The text of one of the bot's messages.
/// </summary>
/// <param name="Kind">Which message.</param>
/// <param name="Text">Telegram HTML with placeholders, see <see cref="Placeholders"/>.</param>
/// <param name="IsDefault">True while no admin has saved a text: it comes from appsettings.</param>
/// <param name="UpdatedAt">When an admin last saved it (UTC), null for the default.</param>
public record MessageTemplateDto(MessageTemplateKind Kind, string Text, bool IsDefault, DateTime? UpdatedAt)
{
    /// <summary>
    /// Telegram counts 4096 characters after the placeholders are filled in and the tags are removed.
    /// </summary>
    public const int TextMaxLength = 4000;

    /// <summary>
    /// Replaced for every recipient.
    /// </summary>
    public IReadOnlyList<string> Placeholders => MessagePlaceholders.For(Kind);
}
