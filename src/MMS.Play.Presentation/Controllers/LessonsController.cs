using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MMS.Play.Contracts.Authorization;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Lessons;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Notifications;
using MMS.Play.Contracts.Models.Lessons;
using MMS.Play.Contracts.Models.Notifications;
using MMS.Play.Presentation.Controllers.Base;
using MMS.Play.Presentation.Models.Requests.Lessons;
using MMS.Play.Presentation.Models.Responses.Lessons;

namespace MMS.Play.Presentation.Controllers;

/// <summary>
/// The lesson timetable: lessons in rooms by week, payments, earnings and the lesson settings. Admins only.
/// Dates and times are school time ("Lessons:TimeZoneId"), dates as "2026-11-12", times as "14:00:00".
/// </summary>
[ApiVersion(1)]
[Authorize(Roles = Roles.Admin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public class LessonsController(
    ILessonService lessonService,
    ILessonStatsService statsService,
    ILessonSettingsService settingsService,
    ILessonRescheduleService rescheduleService,
    ILessonPaymentService paymentService,
    IPaymentReminderService paymentReminderService) : BaseController
{
    /// <summary>
    /// Who paid for their lessons and who did not, per student, over a period.
    /// </summary>
    /// <remarks>
    /// A lesson is due (a debt) once it has started and is not paid; unpaid lessons still to come are counted separately.
    /// Rescheduled lessons are left out. Students owing the most come first.
    /// </remarks>
    /// <param name="from">First day, "2026-11-01"; no limit when left out.</param>
    /// <param name="to">Last day; no limit when left out.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The students with their lessons and totals.</response>
    /// <response code="400">from is after to.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpGet("payments")]
    [ProducesResponseType<PaymentOverviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PaymentOverviewDto> GetPayments([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken) =>
        paymentService.GetOverviewAsync(from, to, cancellationToken);

    /// <summary>
    /// Marks several lessons as paid or unpaid at once, e.g. everything a student owes.
    /// </summary>
    /// <response code="200">How many lessons changed; already paid ones keep their payment time.</response>
    /// <response code="400">No lessons or more than 500.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpPut("payments")]
    [ProducesResponseType<SetLessonsPaidResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<SetLessonsPaidResponse> SetLessonsPaid(SetLessonsPaidRequest request, CancellationToken cancellationToken) =>
        new(await paymentService.SetPaidAsync(request.LessonIds, request.IsPaid!.Value, cancellationToken));

    /// <summary>
    /// Sends a payment reminder about a student right away: to the student and to each parent.
    /// </summary>
    /// <remarks>
    /// Only those subscribed in the bot with «Reminders» on get it; a parent is reminded with the parent text. Counts as the
    /// latest payment reminder, so the scheduled one waits "every N days" from now.
    /// </remarks>
    /// <param name="studentId">The student's subscriber identifier.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">How many messages were sent.</response>
    /// <response code="400">The student owes nothing, nobody can get the message, or the bot is turned off.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpPost("payments/{studentId:guid}/remind")]
    [ProducesResponseType<LessonReminderResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<LessonReminderResult> RemindToPay(Guid studentId, CancellationToken cancellationToken) =>
        paymentReminderService.RemindStudentAsync(studentId, cancellationToken);

    /// <summary>
    /// Sends a payment reminder about every student who owes right away: to the student and to each parent.
    /// </summary>
    /// <remarks>
    /// Like «Remind» for one student, for all of them at once: any period, whenever they were reminded last, and it
    /// counts as the latest payment reminder. Only those subscribed in the bot with «Reminders» on get it.
    /// Takes a moment with many recipients: messages go out at the rate Telegram allows.
    /// </remarks>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">How many students owe, how many of them nobody could be told about, and the messages sent.</response>
    /// <response code="400">Nobody owes, or the bot is turned off.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpPost("payments/remind-all")]
    [ProducesResponseType<PaymentRemindAllResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PaymentRemindAllResult> RemindAllToPay(CancellationToken cancellationToken) =>
        paymentReminderService.RemindAllAsync(cancellationToken);

    /// <summary>
    /// Returns a week of the timetable: rooms with their hours, teachers and lessons.
    /// </summary>
    /// <param name="date">Any day of the week (Monday to Sunday), the current week when left out.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The week.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpGet("week")]
    [ProducesResponseType<WeekScheduleDto>(StatusCodes.Status200OK)]
    public Task<WeekScheduleDto> GetWeek([FromQuery] DateOnly? date, CancellationToken cancellationToken) =>
        lessonService.GetWeekAsync(date, cancellationToken);

    /// <summary>
    /// Copies the lessons of the week before into a week: same days, times, rooms, teachers, students and prices, not paid.
    /// </summary>
    /// <remarks>
    /// The regular timetable is copied: a cancelled lesson comes back as scheduled, a rescheduled one at its original time
    /// (the lesson that replaced it is left out). Skipped: lessons already in the week, lessons whose time has come
    /// (e.g. Monday when copying on Wednesday) and lessons that would clash. Copying twice creates nothing the second time.
    /// </remarks>
    /// <response code="200">The created lessons and the skipped ones with the reason.</response>
    /// <response code="400">"date" is missing.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpPost("week/copy-previous")]
    [ProducesResponseType<CopyWeekResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<CopyWeekResultDto> CopyPreviousWeek(CopyPreviousWeekRequest request, CancellationToken cancellationToken) =>
        lessonService.CopyPreviousWeekAsync(request.Date!.Value, cancellationToken);

    /// <summary>
    /// Lists the Telegram subscribers by name, the students a lesson can be given to (and a parent's children can be picked from).
    /// </summary>
    /// <response code="200">Every subscriber but the parents, unsubscribed ones included (isSubscribed: false).</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpGet("students")]
    [ProducesResponseType<IReadOnlyList<StudentOptionDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<StudentOptionDto>> ListStudents(CancellationToken cancellationToken) =>
        lessonService.ListStudentsAsync(cancellationToken);

    /// <summary>
    /// Creates a lesson, or the same lesson for several weeks in a row.
    /// </summary>
    /// <remarks>
    /// A room, a teacher and a student can only be in one lesson at a time: a lesson starting less than 60 minutes
    /// before or after another one of the same room, teacher or student is refused. With repeatWeeks every week is
    /// checked first, nothing is created when one of them clashes.
    /// </remarks>
    /// <response code="201">The created lessons, by date.</response>
    /// <response code="400">Invalid values, an unknown room, teacher or student, or a clash; errors say which.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpPost]
    [ProducesResponseType<IReadOnlyList<LessonDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Create(CreateLessonRequest request, CancellationToken cancellationToken)
    {
        var input = request.ToInput(await settingsService.GetAsync(cancellationToken));
        var lessons = await lessonService.CreateAsync(input, request.RepeatWeeks, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, lessons);
    }

    /// <summary>
    /// Changes a lesson: moves it, gives it to another teacher or student, changes its price or payment.
    /// </summary>
    /// <remarks>
    /// With scope "Following" the later lessons of the series that have not started change too, all or nothing:
    /// a clash of any of them refuses the whole change. Cancelled and rescheduled ones are left as they are.
    /// </remarks>
    /// <param name="id">Lesson identifier.</param>
    /// <param name="request">All fields of the lesson.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The updated lesson.</response>
    /// <response code="400">See creating a lesson.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no lesson with this identifier.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<LessonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<LessonDto>> Update(Guid id, UpdateLessonRequest request, CancellationToken cancellationToken)
    {
        var lesson = await lessonService.UpdateAsync(id, request.ToInput(), request.Scope, cancellationToken);
        return lesson is null ? LessonNotFound(id) : lesson;
    }

    /// <summary>
    /// Marks a lesson as paid or not paid.
    /// </summary>
    /// <param name="id">Lesson identifier.</param>
    /// <param name="request">The new value.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The updated lesson.</response>
    /// <response code="400">"isPaid" is missing.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no lesson with this identifier.</response>
    [HttpPut("{id:guid}/payment")]
    [ProducesResponseType<LessonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<LessonDto>> SetPaid(Guid id, SetLessonPaidRequest request, CancellationToken cancellationToken)
    {
        var lesson = await lessonService.SetPaidAsync(id, request.IsPaid!.Value, cancellationToken);
        return lesson is null ? LessonNotFound(id) : lesson;
    }

    /// <summary>
    /// Deletes a lesson, or ends its weekly series.
    /// </summary>
    /// <param name="id">Lesson identifier.</param>
    /// <param name="scope">"Following" also deletes the later lessons of the series from today on (the past keeps its
    /// history); "This" by default.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="204">The lesson was deleted.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no lesson with this identifier.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken, [FromQuery] LessonChangeScope scope = LessonChangeScope.This) =>
        await lessonService.DeleteAsync(id, scope, cancellationToken) ? NoContent() : LessonNotFound(id);

    /// <summary>
    /// Cancels a lesson that has not started yet.
    /// </summary>
    /// <remarks>
    /// The lesson stays in the timetable, crossed out, and counts nowhere (earnings, debts, reminders, clashes); its pending
    /// reschedule request is closed. The student and the parents are told in the bot. A cancellation less than 24 hours
    /// before the start is late and counted as such in the statistics. Students and parents can cancel in the bot too.
    /// </remarks>
    /// <param name="id">Lesson identifier.</param>
    /// <param name="request">The reason, optional.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The cancelled lesson.</response>
    /// <response code="400">Already cancelled, rescheduled or started.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no lesson with this identifier.</response>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<LessonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<LessonDto>> Cancel(Guid id, CancelLessonRequest request, CancellationToken cancellationToken)
    {
        var lesson = await lessonService.CancelAsync(id, request.Reason, User.Identity?.Name, cancellationToken);
        return lesson is null ? LessonNotFound(id) : lesson;
    }

    /// <summary>
    /// Brings a cancelled lesson back.
    /// </summary>
    /// <param name="id">Lesson identifier.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">The lesson, scheduled again.</response>
    /// <response code="400">Not cancelled, or the room, the teacher or the student is busy at that time by now.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no lesson with this identifier.</response>
    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType<LessonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<LessonDto>> Restore(Guid id, CancellationToken cancellationToken)
    {
        var lesson = await lessonService.RestoreAsync(id, cancellationToken);
        return lesson is null ? LessonNotFound(id) : lesson;
    }

    /// <summary>
    /// Returns lessons and earnings of the day, week, month, quarter and year containing a date.
    /// </summary>
    /// <remarks>
    /// Earned (paidAmount) is the sum of the prices of the lessons marked as paid, unpaidAmount what is still expected.
    /// A lesson counts in the period of its date, not of its payment.
    /// </remarks>
    /// <param name="date">The day the periods are around, today when left out.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">One entry per period, from the day to the year.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpGet("stats")]
    [ProducesResponseType<LessonStatsDto>(StatusCodes.Status200OK)]
    public Task<LessonStatsDto> GetStats([FromQuery] DateOnly? date, CancellationToken cancellationToken) =>
        statsService.GetAsync(date, cancellationToken);

    /// <summary>
    /// Returns the default lesson price. The bot's schedule is GET /api/v1/telegram/settings.
    /// </summary>
    /// <response code="200">The saved settings, or the appsettings defaults (isDefault: true) while none are saved.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpGet("settings")]
    [ProducesResponseType<LessonSettingsDto>(StatusCodes.Status200OK)]
    public Task<LessonSettingsDto> GetSettings(CancellationToken cancellationToken) =>
        settingsService.GetAsync(cancellationToken);

    /// <summary>
    /// Saves the default lesson price.
    /// </summary>
    /// <remarks>
    /// A new price applies to lessons created from now on. The bot's schedule is PUT /api/v1/telegram/settings.
    /// </remarks>
    /// <response code="200">The saved settings.</response>
    /// <response code="400">The price is out of range.</response>
    /// <response code="403">The user is not an admin.</response>
    [HttpPut("settings")]
    [ProducesResponseType<LessonSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<LessonSettingsDto> UpdateSettings(UpdateLessonSettingsRequest request, CancellationToken cancellationToken) =>
        settingsService.UpdateAsync(request.ToUpdate(), cancellationToken);

    /// <summary>
    /// Approves a time a student proposed in the bot: the lesson moves there.
    /// </summary>
    /// <remarks>
    /// The old lesson stays as "Rescheduled" (crossed out, counted nowhere), a new one takes the proposed time and
    /// the payment. The student and the bot admins are told in Telegram. When the time got taken in the meantime the
    /// request is declined instead (decision "SlotTaken") and the student is asked to pick another time.
    /// </remarks>
    /// <param name="requestId">Reschedule request identifier, from the lesson's pendingReschedule.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">What happened: Approved or SlotTaken.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no such request.</response>
    /// <response code="409">Already answered (in the bot or by another admin) or withdrawn by the student.</response>
    [HttpPost("reschedule-requests/{requestId:guid}/approve")]
    [ProducesResponseType<RescheduleDecisionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public Task<IActionResult> ApproveReschedule(Guid requestId, CancellationToken cancellationToken) =>
        DecideAsync(requestId, approve: true, cancellationToken);

    /// <summary>
    /// Declines a time a student proposed in the bot; the student is asked to pick another one.
    /// </summary>
    /// <param name="requestId">Reschedule request identifier, from the lesson's pendingReschedule.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <response code="200">Declined.</response>
    /// <response code="403">The user is not an admin.</response>
    /// <response code="404">There is no such request.</response>
    /// <response code="409">Already answered (in the bot or by another admin) or withdrawn by the student.</response>
    [HttpPost("reschedule-requests/{requestId:guid}/decline")]
    [ProducesResponseType<RescheduleDecisionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public Task<IActionResult> DeclineReschedule(Guid requestId, CancellationToken cancellationToken) =>
        DecideAsync(requestId, approve: false, cancellationToken);

    private async Task<IActionResult> DecideAsync(Guid requestId, bool approve, CancellationToken cancellationToken)
    {
        // shown to the bot admins under the request: "✅ Погоджено — admin (сайт)"
        var decidedBy = $"{User.Identity?.Name ?? "admin"} (сайт)";
        var decision = await rescheduleService.DecideAsync(requestId, approve, decidedBy, cancellationToken);

        return decision switch
        {
            RescheduleDecision.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Request not found",
                detail: $"Reschedule request '{requestId}' does not exist."),
            RescheduleDecision.AlreadyDecided => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Already answered",
                detail: "The request was already answered or the student withdrew it."),
            _ => Ok(new RescheduleDecisionResponse(decision))
        };
    }

    private ObjectResult LessonNotFound(Guid id) =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Lesson not found", detail: $"Lesson '{id}' does not exist.");
}
