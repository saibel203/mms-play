namespace MMS.Play.Contracts.Models.Notifications;

/// <summary>
/// Limits and placeholders of the broadcasts for parents.
/// </summary>
public static class ParentBroadcastLimits
{
    /// <summary>
    /// Telegram allows 4096 characters, some room is left for the answer hint and the answer.
    /// </summary>
    public const int TextMaxLength = 3500;

    /// <summary>
    /// Answer buttons, one per row.
    /// </summary>
    public const int MaxOptions = 8;

    /// <summary>
    /// Longer button texts are cut off by Telegram on phones.
    /// </summary>
    public const int OptionMaxLength = 40;

    public const int MaxRecipients = 500;

    /// <summary>
    /// Longer typed answers are cut.
    /// </summary>
    public const int AnswerMaxLength = 1000;

    /// <summary>
    /// A typed message that is not a reply to a question answers the latest unanswered question of the chat,
    /// if it was sent within this many days.
    /// </summary>
    public const int TextAnswerWindowDays = 7;

    /// <summary>
    /// The parent's first name.
    /// </summary>
    public const string FirstNamePlaceholder = MessagePlaceholders.FirstName;

    /// <summary>
    /// The child of the lesson; when sent to every parent, the names of all their children ("Олег, Марія").
    /// </summary>
    public const string ChildNamePlaceholder = MessagePlaceholders.ChildName;

    /// <summary>
    /// Work with every audience.
    /// </summary>
    public static readonly IReadOnlyList<string> CommonPlaceholders = [FirstNamePlaceholder, ChildNamePlaceholder];

    /// <summary>
    /// The same as in the lesson reminder, <see cref="BroadcastAudience.LessonDay"/> only.
    /// </summary>
    public static readonly IReadOnlyList<string> LessonPlaceholders =
    [
        MessagePlaceholders.LessonDay,
        MessagePlaceholders.LessonDate,
        MessagePlaceholders.LessonTime,
        MessagePlaceholders.Room,
        MessagePlaceholders.Teacher
    ];
}
