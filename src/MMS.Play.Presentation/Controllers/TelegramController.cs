using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MMS.Play.Contracts.Authorization;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;
using MMS.Play.Contracts.Models.Common;
using MMS.Play.Contracts.Models.Notifications;
using MMS.Play.Presentation.Controllers.Base;
using MMS.Play.Presentation.Models.Requests.Telegram;
using MMS.Play.Presentation.Models.Responses.Telegram;

namespace MMS.Play.Presentation.Controllers;

/// <summary>
/// Telegram bot administration: who subscribed, who gets reminders and what the bot's messages say. Admins only.
/// Works while the bot is turned off too, the changes apply once it runs.
/// </summary>
[ApiVersion(1)]
[Authorize(Roles = Roles.Admin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public class TelegramController(
    ITelegramSubscriberService subscriberService,
    IMessageTemplateService templateService,
    IInviteService inviteService,
    INotificationSettingsService notificationSettingsService,
    IAdminDigestService adminDigestService) : BaseController
{
    /// <summary>
    /// Returns when and what the bot sends on its own: lesson, payment and homework reminders, the admins' daily
    /// summary, trial lesson booking, the payment link and details.
    /// </summary>
    /// <response code="200">The saved settings, or the appsettings defaults (isDefault: true) while none are saved.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpGet("settings")]
    [ProducesResponseType<NotificationSettingsDto>(StatusCodes.Status200OK)]
    public Task<NotificationSettingsDto> GetSettings(CancellationToken cancellationToken) =>
        notificationSettingsService.GetAsync(cancellationToken);

    /// <summary>
    /// Saves when and what the bot sends on its own.
    /// </summary>
    /// <remarks>
    /// Lesson reminders: from reminderDaysBefore days before a lesson until it starts, at each of reminderTimes
    /// (school time), one message per lesson. Payment reminders: at paymentReminderTime, again every
    /// paymentReminderEveryDays days while unpaid. Homework: once a day at homeworkReminderTime to students who have
    /// not practised yet. Daily summary: at adminDigestTime to the subscribers marked as admin.
    /// </remarks>
    /// <response code="200">The saved settings.</response>
    /// <response code="400">A value is out of range, the reminder times are empty or repeated, or the payment link is not http(s).</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpPut("settings")]
    [ProducesResponseType<NotificationSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<NotificationSettingsDto> UpdateSettings(UpdateNotificationSettingsRequest request, CancellationToken cancellationToken) =>
        notificationSettingsService.UpdateAsync(request.ToUpdate(), cancellationToken);

    /// <summary>
    /// Sends today's summary to the admins in the bot right away, to see what it looks like.
    /// </summary>
    /// <remarks>Does not count as the day's summary: the scheduled one still goes out.</remarks>
    /// <response code="200">How many admins got it.</response>
    /// <response code="400">No subscribed admin, Telegram did not take it, or the bot is turned off.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpPost("digest/send-now")]
    [ProducesResponseType<SendDigestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<SendDigestResponse> SendDigestNow(CancellationToken cancellationToken) =>
        new(await adminDigestService.SendNowAsync(cancellationToken));

    /// <summary>
    /// Lists the invite links, latest first.
    /// </summary>
    /// <remarks>
    /// A person opens the link in Telegram and presses Start: they are subscribed and get the invite's role
    /// (a parent is linked to its children). link is null while Telegram:BotUsername is not set.
    /// </remarks>
    /// <response code="200">The invites with who used them.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpGet("invites")]
    [ProducesResponseType<IReadOnlyList<InviteDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<InviteDto>> ListInvites(CancellationToken cancellationToken) =>
        inviteService.ListAsync(cancellationToken);

    /// <summary>
    /// Creates an invite link for a student or a parent.
    /// </summary>
    /// <response code="201">The invite with its link.</response>
    /// <response code="400">Children on a student invite, unknown children or parents among them, limits out of range.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpPost("invites")]
    [ProducesResponseType<InviteDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> CreateInvite(CreateInviteRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await inviteService.CreateAsync(request.ToInput(), cancellationToken));

    /// <summary>
    /// Deletes an invite: the link stops working, who used it keeps their role.
    /// </summary>
    /// <param name="id">Invite identifier.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no invite with this identifier.</response>
    [HttpDelete("invites/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> DeleteInvite(Guid id, CancellationToken cancellationToken) =>
        await inviteService.DeleteAsync(id, cancellationToken)
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Invite not found", detail: $"Invite '{id}' does not exist.");

    /// <summary>
    /// Lists users who pressed Start in the bot, latest first.
    /// </summary>
    /// <remarks>
    /// A user gets reminders only when both isSubscribed (the user's choice in the bot) and remindersEnabled (the admin's checkbox) are true.
    /// </remarks>
    /// <response code="200">The requested page, empty when it is past the last one.</response>
    /// <response code="400">Invalid paging or filter parameters.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpGet("subscribers")]
    [ProducesResponseType<PagedResult<TelegramSubscriberDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PagedResult<TelegramSubscriberDto>> ListSubscribers(
        [FromQuery] ListTelegramSubscribersRequest request,
        CancellationToken cancellationToken) =>
        subscriberService.ListAsync(request.ToQuery(), cancellationToken);

    /// <summary>
    /// Turns lesson reminders on or off for one subscriber.
    /// </summary>
    /// <remarks>
    /// Kept when the user unsubscribes and subscribes again in the bot.
    /// </remarks>
    /// <param name="id">Subscriber identifier.</param>
    /// <param name="request">The new checkbox value.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The updated subscriber.</response>
    /// <response code="400">"enabled" is missing.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no subscriber with this identifier.</response>
    [HttpPut("subscribers/{id:guid}/reminders")]
    [ProducesResponseType<TelegramSubscriberDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<TelegramSubscriberDto>> SetRemindersEnabled(
        Guid id,
        SetRemindersEnabledRequest request,
        CancellationToken cancellationToken)
    {
        var subscriber = await subscriberService.SetRemindersEnabledAsync(id, request.Enabled!.Value, cancellationToken);
        return subscriber is null ? SubscriberNotFound(id) : subscriber;
    }

    /// <summary>
    /// Makes a subscriber a bot admin or takes it away.
    /// </summary>
    /// <remarks>
    /// Bot admins get the students' reschedule requests in Telegram with "yes" / "no" buttons.
    /// Requests can also be answered on the timetable page, with or without bot admins.
    /// </remarks>
    /// <param name="id">Subscriber identifier.</param>
    /// <param name="request">The new checkbox value.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The updated subscriber.</response>
    /// <response code="400">"isAdmin" is missing.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no subscriber with this identifier.</response>
    [HttpPut("subscribers/{id:guid}/admin")]
    [ProducesResponseType<TelegramSubscriberDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<TelegramSubscriberDto>> SetAdmin(Guid id, SetSubscriberAdminRequest request, CancellationToken cancellationToken)
    {
        var subscriber = await subscriberService.SetAdminAsync(id, request.IsAdmin!.Value, cancellationToken);
        return subscriber is null ? SubscriberNotFound(id) : subscriber;
    }

    /// <summary>
    /// Makes a subscriber a parent (or not) and links their children.
    /// </summary>
    /// <remarks>
    /// Parents get the broadcasts for parents (POST broadcasts), about their children's lessons too. Children are students:
    /// subscribers who are not parents. childIds replaces the linked children; they are kept when isParent is false.
    /// </remarks>
    /// <param name="id">Subscriber identifier.</param>
    /// <param name="request">The checkbox and the children.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The updated subscriber.</response>
    /// <response code="400">"isParent" is missing, or a child is unknown, a parent or the subscriber itself.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no subscriber with this identifier.</response>
    [HttpPut("subscribers/{id:guid}/parent")]
    [ProducesResponseType<TelegramSubscriberDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<TelegramSubscriberDto>> SetParent(Guid id, SetSubscriberParentRequest request, CancellationToken cancellationToken)
    {
        var subscriber = await subscriberService.SetParentAsync(id, request.IsParent!.Value, request.ChildIds, cancellationToken);
        return subscriber is null ? SubscriberNotFound(id) : subscriber;
    }

    private ObjectResult SubscriberNotFound(Guid id) =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Subscriber not found", detail: $"Subscriber '{id}' does not exist.");

    /// <summary>
    /// Returns the texts of every message the bot sends on its own.
    /// </summary>
    /// <remarks>
    /// LessonReminder (to a student before a lesson), ParentLessonReminder (to a parent before a child's lesson),
    /// PaymentReminder (to a student who owes) and ParentPaymentReminder (to a parent whose child owes).
    /// </remarks>
    /// <response code="200">One per kind: the saved text, or the appsettings default (isDefault: true) while none is saved.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpGet("templates")]
    [ProducesResponseType<IReadOnlyList<MessageTemplateDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<MessageTemplateDto>> ListTemplates(CancellationToken cancellationToken) =>
        templateService.ListAsync(cancellationToken);

    /// <summary>
    /// Saves the text of a message, the next one of this kind uses it.
    /// </summary>
    /// <remarks>
    /// The text is Telegram HTML and is checked before it is saved: Telegram would reject every message with broken markup.
    /// Supported tags: b, i, u, s, a href, code, pre, blockquote, tg-spoiler. Write &amp;lt; &amp;gt; &amp;amp; to show &lt; &gt; &amp; as text.
    /// The placeholders of each kind are in its "placeholders".
    /// </remarks>
    /// <param name="kind">Which message.</param>
    /// <param name="request">The new text.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The saved text.</response>
    /// <response code="400">The text is empty, too long or its markup is not accepted by Telegram, errors.text says why.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpPut("templates/{kind}")]
    [ProducesResponseType<MessageTemplateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<MessageTemplateDto> UpdateTemplate(MessageTemplateKind kind, UpdateMessageTemplateRequest request, CancellationToken cancellationToken) =>
        templateService.UpdateAsync(kind, request.Text, cancellationToken);

    /// <summary>
    /// Removes the saved text of a message, the appsettings default is used again.
    /// </summary>
    /// <param name="kind">Which message.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The default text.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpDelete("templates/{kind}")]
    [ProducesResponseType<MessageTemplateDto>(StatusCodes.Status200OK)]
    public Task<MessageTemplateDto> ResetTemplate(MessageTemplateKind kind, CancellationToken cancellationToken) =>
        templateService.ResetAsync(kind, cancellationToken);
}
