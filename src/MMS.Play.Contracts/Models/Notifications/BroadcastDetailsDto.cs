namespace MMS.Play.Contracts.Models.Notifications;

/// <summary>
/// A broadcast with every message and answer.
/// </summary>
/// <param name="Id">Broadcast identifier.</param>
/// <param name="CreatedAt">When it was sent (UTC).</param>
/// <param name="CreatedBy">The admin's user name.</param>
/// <param name="Audience">Who it was for.</param>
/// <param name="LessonDate">The lesson day of <see cref="BroadcastAudience.LessonDay"/>.</param>
/// <param name="Text">As written, with placeholders.</param>
/// <param name="Options">Answer buttons.</param>
/// <param name="AllowTextAnswer">Typed answers are accepted.</param>
/// <param name="Deliveries">One per message, by parent name.</param>
public record BroadcastDetailsDto(
    Guid Id,
    DateTime CreatedAt,
    string? CreatedBy,
    BroadcastAudience Audience,
    DateOnly? LessonDate,
    string Text,
    IReadOnlyList<string> Options,
    bool AllowTextAnswer,
    IReadOnlyList<BroadcastDeliveryDto> Deliveries);

/// <summary>
/// One message of a broadcast and the parent's answer.
/// </summary>
/// <param name="Id">Delivery identifier.</param>
/// <param name="ParentId">The parent's subscriber identifier.</param>
/// <param name="ParentName">First and last name.</param>
/// <param name="Username">Telegram username without "@".</param>
/// <param name="ChildName">As in the message.</param>
/// <param name="LessonDate">The lesson the message was about.</param>
/// <param name="LessonTime">The lesson the message was about.</param>
/// <param name="Status">Whether Telegram took the message.</param>
/// <param name="Error">Why Telegram refused it.</param>
/// <param name="SentAt">UTC.</param>
/// <param name="Answer">The pressed button or the typed text, the latest one.</param>
/// <param name="AnswerOption">Index of the pressed button, null for a typed answer.</param>
/// <param name="AnsweredAt">UTC.</param>
public record BroadcastDeliveryDto(
    Guid Id,
    Guid ParentId,
    string ParentName,
    string? Username,
    string? ChildName,
    DateOnly? LessonDate,
    TimeOnly? LessonTime,
    BroadcastDeliveryStatus Status,
    string? Error,
    DateTime? SentAt,
    string? Answer,
    int? AnswerOption,
    DateTime? AnsweredAt);
