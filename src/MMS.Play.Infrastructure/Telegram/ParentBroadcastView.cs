using System.Net;
using Telegram.Bot.Types.ReplyMarkups;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// How a broadcast for parents looks in the bot: the text, a hint how to answer, the answer buttons
/// and, once answered, the answer under the text with the pressed button ticked.
/// </summary>
public static class ParentBroadcastView
{
    /// <summary>
    /// "ba:{deliveryId}:{optionIndex}": a parent pressed an answer button. 2 + 1 + 32 + 1 + 1 bytes, well under Telegram's 64.
    /// </summary>
    public const string Answer = "ba";

    public static bool IsAnswerCallback(string data) => data.StartsWith(Answer + ":", StringComparison.Ordinal);

    public static bool TryParseAnswer(string data, out Guid deliveryId, out int optionIndex)
    {
        deliveryId = Guid.Empty;
        optionIndex = -1;

        var parts = data.Split(':');
        return parts.Length == 3
               && parts[0] == Answer
               && Guid.TryParseExact(parts[1], "N", out deliveryId)
               && int.TryParse(parts[2], out optionIndex);
    }

    /// <summary>
    /// The message text: the filled in broadcast, how to answer it and the answer given so far.
    /// </summary>
    public static string Text(string text, IReadOnlyList<string> options, bool allowTextAnswer, string? answer)
    {
        var hint = (options.Count > 0, allowTextAnswer) switch
        {
            (true, true) => "\n\n<i>Оберіть варіант або напишіть свою відповідь повідомленням.</i>",
            (false, true) => "\n\n<i>✍️ Напишіть відповідь повідомленням.</i>",
            _ => ""
        };

        return answer is null
            ? text + hint
            : text + hint + $"\n\n✅ Ваша відповідь: <b>{WebUtility.HtmlEncode(answer)}</b>";
    }

    /// <summary>
    /// One button per answer, the chosen one ticked; they stay after answering, so the answer can be changed.
    /// </summary>
    public static InlineKeyboardMarkup? Keyboard(Guid deliveryId, IReadOnlyList<string> options, int? chosen)
    {
        if (options.Count == 0)
            return null;

        var rows = options.Select((option, index) => new[]
        {
            InlineKeyboardButton.WithCallbackData(index == chosen ? $"✅ {option}" : option, $"{Answer}:{deliveryId:N}:{index}")
        });
        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>
    /// The markup of a new message. A question answered by typing only opens the reply field right away.
    /// </summary>
    public static ReplyMarkup? NewMessageMarkup(Guid deliveryId, IReadOnlyList<string> options, bool allowTextAnswer) =>
        (ReplyMarkup?)Keyboard(deliveryId, options, chosen: null)
        ?? (allowTextAnswer ? new ForceReplyMarkup { InputFieldPlaceholder = "Ваша відповідь" } : null);
}
