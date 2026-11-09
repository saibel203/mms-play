using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.Models.Notifications;

namespace MMS.Play.Presentation.Models.Requests.Telegram;

/// <summary>
/// New text of one of the bot's messages.
/// </summary>
public class UpdateMessageTemplateRequest
{
    /// <summary>
    /// Telegram HTML: b, i, u, s, a, code, pre, blockquote, tg-spoiler tags, with the placeholders of the message.
    /// Write &amp;lt; &amp;gt; &amp;amp; to show &lt; &gt; &amp; as text.
    /// </summary>
    [Required]
    [StringLength(MessageTemplateDto.TextMaxLength)]
    public string Text { get; set; } = string.Empty;
}
