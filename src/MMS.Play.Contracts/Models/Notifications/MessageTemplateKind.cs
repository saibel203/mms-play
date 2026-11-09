namespace MMS.Play.Contracts.Models.Notifications;

/// <summary>
/// The messages the bot sends on its own, each with a text an admin can edit.
/// </summary>
public enum MessageTemplateKind
{
    /// <summary>
    /// To a student before a lesson.
    /// </summary>
    LessonReminder,

    /// <summary>
    /// To a parent before a lesson of their child.
    /// </summary>
    ParentLessonReminder,

    /// <summary>
    /// To a student with lessons that took place and are not paid.
    /// </summary>
    PaymentReminder,

    /// <summary>
    /// To a parent whose child has lessons that took place and are not paid.
    /// </summary>
    ParentPaymentReminder,

    /// <summary>
    /// To a student when an admin gives them homework.
    /// </summary>
    HomeworkAssigned,

    /// <summary>
    /// To a student with active homework not practised today, once a day.
    /// </summary>
    HomeworkReminder
}
