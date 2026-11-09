namespace MMS.Play.Infrastructure.Persistence.Entities;

/// <summary>
/// A Telegram user who pressed Start in the bot. Not linked to an application <see cref="User"/> yet.
/// Gets reminders while <see cref="IsActive"/> and <see cref="RemindersEnabled"/> are both true.
/// </summary>
public class TelegramSubscriber
{
    /// <summary>
    /// Version 7 GUID: time ordered, so inserts do not fragment the clustered index.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Telegram user id: permanent, unlike the username. The key used to find the subscriber.
    /// </summary>
    public long TelegramUserId { get; set; }

    /// <summary>
    /// Where messages are sent. In a private chat it equals <see cref="TelegramUserId"/>, but Telegram does not promise that.
    /// </summary>
    public long ChatId { get; set; }

    /// <summary>
    /// Optional in Telegram and can change at any time, refreshed on every /start.
    /// </summary>
    public string? Username { get; set; }

    public required string FirstName { get; set; }
    public string? LastName { get; set; }
    public string? LanguageCode { get; set; }

    /// <summary>
    /// False after /stop or when the user blocked the bot. Only active subscribers get messages. Changed by the user only.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Admin's switch for lesson reminders, kept when the user unsubscribes and subscribes again.
    /// </summary>
    public bool RemindersEnabled { get; set; } = true;

    /// <summary>
    /// Gets the students' reschedule requests in the bot and may answer them there. Set by an admin on the website.
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// A parent: gets the broadcasts for parents, linked to their <see cref="Children"/>. Set by an admin on the website.
    /// </summary>
    public bool IsParent { get; set; }

    /// <summary>
    /// The students this parent is responsible for, kept when <see cref="IsParent"/> is switched off.
    /// </summary>
    public List<TelegramSubscriber> Children { get; set; } = [];

    /// <summary>
    /// The other side of <see cref="Children"/>: the parents of this student.
    /// </summary>
    public List<TelegramSubscriber> Parents { get; set; } = [];

    /// <summary>
    /// Secret of the personal links the bot gives out (the calendar feed, the practice page), so they open without
    /// signing in. Created when first needed; replacing it makes the old links stop working.
    /// </summary>
    public string? AccessToken { get; set; }

    public DateTime SubscribedAtUtc { get; set; }
    public DateTime? UnsubscribedAtUtc { get; set; }
    public DateTime? LastNotifiedAtUtc { get; set; }
}
