namespace MMS.Play.Contracts.Models.Notifications;

/// <summary>
/// A broadcast to send. Without options and typed answers it is a plain message, otherwise a question.
/// </summary>
/// <param name="Audience">Who it is for, decides which placeholders work.</param>
/// <param name="LessonDate">The lesson day of <see cref="BroadcastAudience.LessonDay"/>.</param>
/// <param name="Text">Telegram HTML with placeholders.</param>
/// <param name="Options">Answer buttons.</param>
/// <param name="AllowTextAnswer">The parent may type an answer.</param>
/// <param name="Recipients">The recipients the admin left checked, out of the recipient list.</param>
public record BroadcastInput(
    BroadcastAudience Audience,
    DateOnly? LessonDate,
    string Text,
    IReadOnlyList<string> Options,
    bool AllowTextAnswer,
    IReadOnlyList<BroadcastTarget> Recipients);

/// <param name="ParentId">The parent's subscriber identifier.</param>
/// <param name="LessonId">The lesson with <see cref="BroadcastAudience.LessonDay"/>, null otherwise.</param>
public record BroadcastTarget(Guid ParentId, Guid? LessonId);
