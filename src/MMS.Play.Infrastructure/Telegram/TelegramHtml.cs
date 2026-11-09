using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// Checks text sent with parse_mode=HTML. Telegram rejects the whole message on a markup error,
/// so a broken template would fail every reminder: it is checked when saved instead.
/// See https://core.telegram.org/bots/api#html-style
/// </summary>
public static partial class TelegramHtml
{
    private static readonly FrozenSet<string> AllowedTags = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "b", "strong", "i", "em", "u", "ins", "s", "strike", "del", "span", "tg-spoiler", "a", "code", "pre", "blockquote", "tg-emoji");

    // \G anchors the match at the position passed to Match(text, position)
    [GeneratedRegex(@"\G<(?<closing>/)?(?<name>[a-zA-Z][a-zA-Z0-9-]*)(?:\s[^<>]*)?>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\G&(?:lt|gt|amp|quot|#[0-9]+|#x[0-9a-fA-F]+);")]
    private static partial Regex EntityRegex();

    /// <summary>
    /// Null when Telegram accepts the markup, otherwise what is wrong with it.
    /// </summary>
    public static string? Validate(string text)
    {
        var openTags = new Stack<string>();

        for (var position = 0; position < text.Length; position++)
        {
            switch (text[position])
            {
                case '<':
                    var tag = TagRegex().Match(text, position);
                    if (!tag.Success)
                        return $"Unexpected '<' at position {position + 1}, write &lt; to show it as text.";

                    var name = tag.Groups["name"].Value.ToLowerInvariant();
                    if (!AllowedTags.Contains(name))
                        return $"Tag <{name}> is not supported by Telegram. Supported tags: b, i, u, s, a, code, pre, blockquote, tg-spoiler.";

                    if (!tag.Groups["closing"].Success)
                        openTags.Push(name);
                    else if (!openTags.TryPop(out var expected) || expected != name)
                        return $"Closing tag </{name}> at position {position + 1} does not match an opening tag.";

                    position += tag.Length - 1;
                    break;

                case '>':
                    return $"Unexpected '>' at position {position + 1}, write &gt; to show it as text.";

                case '&':
                    var entity = EntityRegex().Match(text, position);
                    if (!entity.Success)
                        return $"Unexpected '&' at position {position + 1}, write &amp; to show it as text.";

                    position += entity.Length - 1;
                    break;
            }
        }

        return openTags.TryPeek(out var unclosed) ? $"Tag <{unclosed}> is not closed." : null;
    }
}
