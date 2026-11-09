namespace MMS.Play.Contracts.Models.Notifications;

/// <summary>
/// Placeholders replaced in the bot's messages, and which message has which.
/// </summary>
public static class MessagePlaceholders
{
    /// <summary>
    /// The recipient's first name: the student, or the parent in a parent's message.
    /// </summary>
    public const string FirstName = "{FirstName}";

    /// <summary>
    /// The student a parent's message is about.
    /// </summary>
    public const string ChildName = "{ChildName}";

    /// <summary>
    /// When, said the way people say it: "сьогодні", "завтра" or "у середу (12.11)". Ukrainian.
    /// </summary>
    public const string LessonDay = "{LessonDay}";

    /// <summary>
    /// Day of the week and date, e.g. "середа, 12.11".
    /// </summary>
    public const string LessonDate = "{LessonDate}";

    /// <summary>
    /// Start time, e.g. "14:00".
    /// </summary>
    public const string LessonTime = "{LessonTime}";

    /// <summary>
    /// Room number, e.g. "314".
    /// </summary>
    public const string Room = "{Room}";

    /// <summary>
    /// Teacher's name.
    /// </summary>
    public const string Teacher = "{Teacher}";

    /// <summary>
    /// How many lessons that took place are not paid, e.g. "3".
    /// </summary>
    public const string UnpaidCount = "{UnpaidCount}";

    /// <summary>
    /// What they cost together, e.g. "1 500 грн".
    /// </summary>
    public const string UnpaidAmount = "{UnpaidAmount}";

    /// <summary>
    /// The unpaid lessons, one per line: "• ср 12.11 о 14:00 — 500 грн".
    /// </summary>
    public const string UnpaidLessons = "{UnpaidLessons}";

    /// <summary>
    /// The payment link from the lesson settings as a clickable link; empty when none is set.
    /// </summary>
    public const string PaymentLink = "{PaymentLink}";

    /// <summary>
    /// The payment details from the lesson settings (recipient, IBAN, purpose); empty when none are set.
    /// </summary>
    public const string PaymentDetails = "{PaymentDetails}";

    /// <summary>
    /// Homework: its title, score, bars, tempo, deadline and what to do; in a reminder every homework not practised today.
    /// </summary>
    public const string Homework = "{Homework}";

    /// <summary>
    /// Deadline of the homework as a line, e.g. "📅 Виконати до пт 14.11"; empty when there is none.
    /// </summary>
    public const string DueDate = "{DueDate}";

    /// <summary>
    /// The student's personal practice page (scores and the player) as a link; empty while PublicSite:BaseUrl is not set.
    /// </summary>
    public const string PracticeLink = "{PracticeLink}";

    /// <summary>
    /// Days in a row the student practised, up to yesterday (today is not done yet), e.g. "3"; "0" for none.
    /// </summary>
    public const string Streak = "{Streak}";

    public static IReadOnlyList<string> For(MessageTemplateKind kind) => kind switch
    {
        MessageTemplateKind.LessonReminder => [FirstName, LessonDay, LessonDate, LessonTime, Room, Teacher],
        MessageTemplateKind.ParentLessonReminder => [FirstName, ChildName, LessonDay, LessonDate, LessonTime, Room, Teacher],
        MessageTemplateKind.PaymentReminder => [FirstName, UnpaidCount, UnpaidAmount, UnpaidLessons, PaymentLink, PaymentDetails],
        MessageTemplateKind.ParentPaymentReminder => [FirstName, ChildName, UnpaidCount, UnpaidAmount, UnpaidLessons, PaymentLink, PaymentDetails],
        MessageTemplateKind.HomeworkAssigned => [FirstName, Homework, DueDate, PracticeLink],
        MessageTemplateKind.HomeworkReminder => [FirstName, Homework, Streak, PracticeLink],
        _ => []
    };
}
