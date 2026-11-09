namespace MMS.Play.Contracts.Models.Notifications;

/// <summary>
/// A sent broadcast in the history.
/// </summary>
/// <param name="Id">Broadcast identifier.</param>
/// <param name="CreatedAt">When it was sent (UTC).</param>
/// <param name="CreatedBy">The admin's user name.</param>
/// <param name="Audience">Who it was for.</param>
/// <param name="LessonDate">The lesson day of <see cref="BroadcastAudience.LessonDay"/>.</param>
/// <param name="Text">As written, with placeholders.</param>
/// <param name="IsQuestion">Has answer buttons or accepts a typed answer.</param>
/// <param name="RecipientCount">Messages to send.</param>
/// <param name="SentCount">Messages Telegram took.</param>
/// <param name="FailedCount">Messages Telegram refused.</param>
/// <param name="AnswerCount">Messages answered.</param>
public record BroadcastSummaryDto(
    Guid Id,
    DateTime CreatedAt,
    string? CreatedBy,
    BroadcastAudience Audience,
    DateOnly? LessonDate,
    string Text,
    bool IsQuestion,
    int RecipientCount,
    int SentCount,
    int FailedCount,
    int AnswerCount);
