using Microsoft.Extensions.Options;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// A subscriber's personal practice page, "{PublicSite}/practice/{access token}": their homework, the scores and the
/// player, opened without signing in.
/// </summary>
public class PracticeLinks(ApplicationDbContext context, IOptions<PublicSiteSettings> siteOptions)
{
    /// <summary>
    /// Telegram takes the link as a button (https, not localhost); otherwise it is written into the text.
    /// </summary>
    public bool CanUseLinkButtons => siteOptions.Value.CanUseLinkButtons;

    /// <summary>
    /// The page of a tracked subscriber, creating their token when they have none; null while PublicSite:BaseUrl is not set.
    /// </summary>
    public async Task<string?> ForAsync(TelegramSubscriber subscriber, CancellationToken cancellationToken)
    {
        var site = siteOptions.Value;
        if (!site.IsConfigured)
            return null;

        var token = await TelegramSubscriberAccessToken.EnsureAsync(context, subscriber, cancellationToken);
        return site.Url($"practice/{token}");
    }
}
