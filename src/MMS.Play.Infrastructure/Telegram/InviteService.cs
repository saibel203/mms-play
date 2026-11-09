using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MMS.Play.Contracts.Exceptions;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;
using MMS.Play.Contracts.Models.Notifications;
using MMS.Play.Infrastructure.Lessons;
using MMS.Play.Infrastructure.Models.Settings;
using MMS.Play.Infrastructure.Persistence;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// Invite links (<see cref="Invite"/>): the admin creates them on the website, the bot redeems them on /start.
/// The deep link payload is "inv_" + the code; Telegram allows 64 characters of A-Z, a-z, 0-9, "_" and "-".
/// </summary>
public class InviteService(ApplicationDbContext context, SchoolClock clock, IOptions<TelegramSettings> telegramOptions) : IInviteService
{
    public const string PayloadPrefix = "inv_";

    private const int MaxChildren = 10;

    public async Task<IReadOnlyList<InviteDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var invites = await context.Invites
            .AsNoTracking()
            .OrderByDescending(invite => invite.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var names = await NamesAsync(invites.SelectMany(invite => invite.ChildIds.Concat(invite.UsedByIds)).Distinct().ToList(), cancellationToken);
        return invites.Select(invite => ToDto(invite, names)).ToList();
    }

    public async Task<InviteDto> CreateAsync(InviteInput input, CancellationToken cancellationToken = default)
    {
        var childIds = (input.ChildIds ?? []).Distinct().ToList();
        var label = string.IsNullOrWhiteSpace(input.Label) ? null : input.Label.Trim();
        var errors = new Dictionary<string, string[]>();

        if (label?.Length > InviteDto.LabelMaxLength)
            errors[nameof(InviteInput.Label)] = [$"The label can be at most {InviteDto.LabelMaxLength} characters long."];
        if (input.MaxUses is < 1 or > InviteDto.MaxMaxUses)
            errors[nameof(InviteInput.MaxUses)] = [$"Allow 1 to {InviteDto.MaxMaxUses} people, or any number."];
        if (input.ValidDays is < 1 or > InviteDto.MaxValidDays)
            errors[nameof(InviteInput.ValidDays)] = [$"Valid for 1 to {InviteDto.MaxValidDays} days, or without a limit."];

        if (input.Role == InviteRole.Student && childIds.Count > 0)
            errors[nameof(InviteInput.ChildIds)] = ["Only a parent invite has children."];
        else if (childIds.Count > MaxChildren)
            errors[nameof(InviteInput.ChildIds)] = [$"A parent can have at most {MaxChildren} children."];
        else if (childIds.Count > 0)
        {
            var students = await context.TelegramSubscribers
                .Where(subscriber => childIds.Contains(subscriber.Id) && !subscriber.IsParent)
                .CountAsync(cancellationToken);
            if (students != childIds.Count)
                errors[nameof(InviteInput.ChildIds)] = ["Some of the children do not exist or are parents."];
        }

        if (errors.Count > 0)
            throw new ValidationFailedException(errors);

        var now = clock.UtcNow;
        var invite = new Invite
        {
            Id = Guid.CreateVersion7(),
            Code = NewCode(),
            Role = input.Role,
            Label = label,
            ChildIds = childIds,
            MaxUses = input.MaxUses,
            CreatedAtUtc = now,
            ExpiresAtUtc = input.ValidDays is { } days ? now.AddDays(days) : null
        };
        context.Invites.Add(invite);
        await context.SaveChangesAsync(cancellationToken);

        return ToDto(invite, await NamesAsync(childIds, cancellationToken));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Invites.Where(invite => invite.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    /// <summary>
    /// Called on /start with an "inv_" payload, after the user was subscribed. Gives the role and links the children.
    /// The same person opening it again changes nothing and uses no extra place.
    /// </summary>
    /// <returns>What to tell the user (Telegram HTML).</returns>
    public async Task<string> RedeemAsync(string code, long telegramUserId, CancellationToken cancellationToken)
    {
        var invite = await context.Invites.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
        var subscriber = await context.TelegramSubscribers
            .Include(x => x.Children)
            .FirstOrDefaultAsync(x => x.TelegramUserId == telegramUserId, cancellationToken);

        if (invite is null || subscriber is null)
            return "⚠️ Це посилання-запрошення недійсне. Попросіть у школи нове.";

        var alreadyUsed = invite.UsedByIds.Contains(subscriber.Id);
        if (!alreadyUsed && !IsActive(invite, clock.UtcNow))
            return "⚠️ Термін дії цього запрошення минув або його вже використали. Попросіть у школи нове посилання.";

        string result;
        if (invite.Role == InviteRole.Parent)
        {
            if (await context.TelegramSubscribers.AnyAsync(parent => parent.Children.Any(child => child.Id == subscriber.Id), cancellationToken))
                return "⚠️ Ви зареєстровані в школі як учень, тому не можете стати батьком/матір'ю за цим посиланням. Зверніться до адміністратора.";

            var children = await context.TelegramSubscribers
                .Where(child => invite.ChildIds.Contains(child.Id) && !child.IsParent && child.Id != subscriber.Id)
                .ToListAsync(cancellationToken);

            subscriber.IsParent = true;
            subscriber.Children.AddRange(children.Where(child => subscriber.Children.All(linked => linked.Id != child.Id)));

            result = children.Count == 0
                ? "👨‍👩‍👧 Вас зареєстровано як одного з батьків. Адміністратор прив'яже вашу дитину."
                : $"👨‍👩‍👧 Вас зареєстровано як одного з батьків: {WebUtility.HtmlEncode(string.Join(", ", children.Select(child => child.FirstName)))}.\n"
                  + "Ви отримуватимете нагадування про уроки й оплату.";
        }
        else
        {
            if (subscriber.IsParent && subscriber.Children.Count > 0)
                return "⚠️ Ви вже зареєстровані як батько/мати. Якщо ви ще й учень, зверніться до адміністратора.";

            subscriber.IsParent = false;
            result = "🎸 Вас зареєстровано як учня школи. Нагадування про уроки приходитимуть сюди.";
        }

        if (!alreadyUsed)
            invite.UsedByIds.Add(subscriber.Id);

        await context.SaveChangesAsync(cancellationToken);
        return result;
    }

    private InviteDto ToDto(Invite invite, IReadOnlyDictionary<Guid, string> names) => new(
        invite.Id,
        Link(invite.Code),
        invite.Role,
        invite.Label,
        invite.ChildIds.Where(names.ContainsKey).Select(id => new LinkedSubscriberDto(id, names[id])).ToList(),
        invite.MaxUses,
        invite.UsedByIds.Where(names.ContainsKey).Select(id => names[id]).ToList(),
        invite.CreatedAtUtc,
        invite.ExpiresAtUtc,
        IsActive(invite, clock.UtcNow));

    private string? Link(string code)
    {
        var username = telegramOptions.Value.BotUsername.Trim().TrimStart('@');
        return username.Length == 0 ? null : $"https://t.me/{username}?start={PayloadPrefix}{code}";
    }

    private static bool IsActive(Invite invite, DateTime now) =>
        (invite.ExpiresAtUtc is null || invite.ExpiresAtUtc > now)
        && (invite.MaxUses is null || invite.UsedByIds.Count < invite.MaxUses);

    private async Task<Dictionary<Guid, string>> NamesAsync(List<Guid> ids, CancellationToken cancellationToken) =>
        ids.Count == 0
            ? []
            : await context.TelegramSubscribers
                .AsNoTracking()
                .Where(subscriber => ids.Contains(subscriber.Id))
                .ToDictionaryAsync(
                    subscriber => subscriber.Id,
                    subscriber => subscriber.LastName == null ? subscriber.FirstName : subscriber.FirstName + " " + subscriber.LastName,
                    cancellationToken);

    // 12 random bytes: 16 URL safe characters, not guessable
    private static string NewCode() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(12)).Replace('+', '-').Replace('/', '_');
}
