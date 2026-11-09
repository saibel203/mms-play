using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace MMS.Play.Infrastructure.Telegram;

/// <summary>
/// The bot's menu: buttons under the message field (a persistent reply keyboard) that send the commands
/// for people who do not type them, and the command list Telegram shows behind the «Меню» button.
/// The keyboard depends on the user: «🎸 Пробний урок» is there for newcomers only (see Trial.TrialEligibility).
/// </summary>
public static class BotMenu
{
    public const string StartCommand = "/start";
    public const string StopCommand = "/stop";
    public const string LessonsCommand = "/lessons";
    public const string PaymentsCommand = "/payments";
    public const string PayCommand = "/pay";
    public const string HomeworkCommand = "/homework";
    public const string CalendarCommand = "/calendar";
    public const string TrialCommand = "/trial";
    public const string ClearCommand = "/clear";
    public const string HelpCommand = "/help";

    public const string ScheduleButton = "📅 Мій розклад";
    public const string DebtButton = "💳 Мій борг";
    public const string PayButton = "💰 Оплатити";
    public const string HomeworkButton = "📚 Домашнє завдання";
    public const string CalendarButton = "📆 Календар";
    public const string TrialButton = "🎸 Пробний урок";
    public const string HelpButton = "❓ Допомога";

    // a reply keyboard button sends its own text as a plain message
    private static readonly Dictionary<string, string> ButtonCommands = new(StringComparer.Ordinal)
    {
        [ScheduleButton] = LessonsCommand,
        [DebtButton] = PaymentsCommand,
        [PayButton] = PayCommand,
        [HomeworkButton] = HomeworkCommand,
        [CalendarButton] = CalendarCommand,
        [TrialButton] = TrialCommand,
        [HelpButton] = HelpCommand
    };

    private static readonly KeyboardButton[][] CommonRows =
    [
        [new KeyboardButton(ScheduleButton), new KeyboardButton(DebtButton)],
        [new KeyboardButton(PayButton), new KeyboardButton(HomeworkButton)],
        [new KeyboardButton(CalendarButton), new KeyboardButton(HelpButton)]
    ];

    private static readonly ReplyKeyboardMarkup Regular = Create(CommonRows);

    // a newcomer sees the trial lesson first: that is what they came for
    private static readonly ReplyKeyboardMarkup Newcomer = Create([[new KeyboardButton(TrialButton)], .. CommonRows]);

    /// <summary>
    /// The menu without the trial button, for messages to people who are known not to be newcomers.
    /// </summary>
    public static ReplyKeyboardMarkup Keyboard => Regular;

    /// <summary>
    /// Rows of two buttons, kept open and sized to their text; with «🎸 Пробний урок» on top for a newcomer.
    /// </summary>
    public static ReplyKeyboardMarkup KeyboardFor(bool canBookTrial) => canBookTrial ? Newcomer : Regular;

    /// <summary>
    /// For SetMyCommands: names without "/", descriptions in Ukrainian.
    /// </summary>
    public static IReadOnlyList<BotCommand> Commands { get; } =
    [
        new() { Command = LessonsCommand[1..], Description = "Мої найближчі уроки, їх можна перенести" },
        new() { Command = PaymentsCommand[1..], Description = "Неоплачені уроки" },
        new() { Command = PayCommand[1..], Description = "Оплатити: сума й реквізити" },
        new() { Command = HomeworkCommand[1..], Description = "Домашнє завдання і тренування" },
        new() { Command = CalendarCommand[1..], Description = "Уроки у вашому календарі (Google, Apple)" },
        new() { Command = TrialCommand[1..], Description = "Записатися на пробний урок" },
        new() { Command = ClearCommand[1..], Description = "Прибрати старі повідомлення з чату" },
        new() { Command = StartCommand[1..], Description = "Підписатися на нагадування" },
        new() { Command = StopCommand[1..], Description = "Відписатися від нагадувань" }
    ];

    /// <summary>
    /// The list of commands shown with /start and /help; the trial line for newcomers only.
    /// </summary>
    public static string HelpText(bool withStart, bool canBookTrial = false) =>
        (canBookTrial ? $"{TrialCommand} або «{TrialButton}» — записатися на безкоштовне знайомство зі школою\n" : "")
        + $"{LessonsCommand} або «{ScheduleButton}» — ваші найближчі уроки, їх можна перенести\n"
        + $"{PaymentsCommand} або «{DebtButton}» — неоплачені уроки\n"
        + $"{PayCommand} або «{PayButton}» — скільки сплатити і куди\n"
        + $"{HomeworkCommand} або «{HomeworkButton}» — що позайматися і сторінка для тренування\n"
        + $"{CalendarCommand} або «{CalendarButton}» — уроки у вашому календарі на телефоні\n"
        + $"{ClearCommand} — прибрати старі повідомлення з чату\n"
        + (withStart ? $"{StartCommand} — підписатися на нагадування\n" : "")
        + $"{StopCommand} — відписатися";

    /// <summary>
    /// The command a menu button stands for, null for any other text.
    /// </summary>
    public static string? ToCommand(string text) => ButtonCommands.GetValueOrDefault(text.Trim());

    private static ReplyKeyboardMarkup Create(IEnumerable<IEnumerable<KeyboardButton>> rows) => new(rows)
    {
        ResizeKeyboard = true,
        IsPersistent = true
    };
}
