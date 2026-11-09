namespace MMS.Play.Infrastructure.Models.Settings;

/// <summary>
/// Bound from the "PublicSite" section in appsettings: where the website (the Angular client, with the API under
/// "/api") is reachable from the internet. Used for links the bot sends: the calendar feed, the practice page,
/// the trial lesson page. Without it the bot does not offer those links.
/// </summary>
public class PublicSiteSettings
{
    public const string SectionName = "PublicSite";

    /// <summary>
    /// "https://school.example.com", no trailing slash needed. Empty = not published.
    /// Telegram refuses link buttons to localhost, so in development the links are sent as text.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// True when <see cref="BaseUrl"/> is set.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);

    /// <summary>
    /// Empty, or an absolute http(s) address.
    /// </summary>
    public bool IsValid =>
        !IsConfigured
        || (Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp));

    /// <summary>
    /// Telegram accepts link buttons only to public https addresses.
    /// </summary>
    public bool CanUseLinkButtons =>
        IsConfigured
        && Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !uri.IsLoopback;

    /// <summary>
    /// The absolute URL of a path on the site: Url("/practice/abc") gives "https://school.example.com/practice/abc".
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="BaseUrl"/> is not set.</exception>
    public string Url(string path) =>
        IsConfigured
            ? $"{BaseUrl!.TrimEnd('/')}/{path.TrimStart('/')}"
            : throw new InvalidOperationException($"{SectionName}:BaseUrl is not set.");
}
