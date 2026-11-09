using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace MMS.Play.Infrastructure.Models.Settings;

/// <summary>
/// Bound from the "Telegram" section in appsettings.
/// </summary>
public partial class TelegramSettings
{
    public const string SectionName = "Telegram";

    /// <summary>
    /// Starts the bot (long polling) and registers the services that send messages. When disabled, nothing talks to Telegram.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Bot username without "@", as registered in @BotFather. Used for t.me links.
    /// </summary>
    [MaxLength(32)]
    public string BotUsername { get; set; } = string.Empty;

    /// <summary>
    /// Token from @BotFather, "123456789:AA...". Keep it out of appsettings: user secrets or the "Telegram__BotToken" environment variable.
    /// </summary>
    public string BotToken { get; set; } = string.Empty;

    public static bool IsValidToken(string? token) => token is not null && TokenRegex().IsMatch(token);

    [GeneratedRegex(@"^\d+:[A-Za-z0-9_-]{30,}$")]
    private static partial Regex TokenRegex();
}
