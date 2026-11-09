using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// The secret in a subscriber's personal links (<see cref="TelegramSubscriber.AccessToken"/>).
/// </summary>
public static class TelegramSubscriberAccessToken
{
    public const int MaxLength = 64;

    // 24 random bytes = 32 URL-safe characters, unguessable
    private const int ByteCount = 24;

    public static string Create() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(ByteCount))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    /// <summary>
    /// The subscriber's token, created and saved when they have none yet.
    /// </summary>
    public static async Task<string> EnsureAsync(ApplicationDbContext context, TelegramSubscriber subscriber, CancellationToken cancellationToken)
    {
        if (subscriber.AccessToken is { } token)
            return token;

        subscriber.AccessToken = Create();
        await context.SaveChangesAsync(cancellationToken);
        return subscriber.AccessToken;
    }

    /// <summary>
    /// The active subscriber with this token, null for an unknown, malformed or replaced one.
    /// </summary>
    public static Task<TelegramSubscriber?> FindAsync(ApplicationDbContext context, string? token, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(token) || token.Length > MaxLength
            ? Task.FromResult<TelegramSubscriber?>(null)
            : context.TelegramSubscribers.FirstOrDefaultAsync(x => x.AccessToken == token && x.IsActive, cancellationToken);
}
