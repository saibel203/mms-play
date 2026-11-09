namespace MMS.Play.Contracts.Models.Lessons;

/// <summary>
/// Who paid for their lessons and who did not, per student, over a period. Rescheduled lessons are left out.
/// A lesson is due once it has started and is not paid; an unpaid lesson still to come is not a debt yet.
/// </summary>
/// <param name="From">First day, null for no limit.</param>
/// <param name="To">Last day, null for no limit.</param>
/// <param name="Today">Today in the school's time zone.</param>
/// <param name="Students">Students owing the most first, then by name; lessons without a student last.</param>
/// <param name="Totals">Over every student.</param>
public record PaymentOverviewDto(
    DateOnly? From,
    DateOnly? To,
    DateOnly Today,
    IReadOnlyList<StudentPaymentsDto> Students,
    PaymentTotalsDto Totals);

/// <param name="StudentId">Null for the lessons without a student (described by a note).</param>
/// <param name="Name">Student name, or "Without a student".</param>
/// <param name="Username">Telegram username without "@".</param>
/// <param name="IsSubscribed">Whether the student can get messages in the bot.</param>
/// <param name="Parents">Names of the linked parents.</param>
/// <param name="LessonCount">Lessons in the period.</param>
/// <param name="PaidCount">Paid lessons.</param>
/// <param name="PaidAmount">What the paid lessons cost.</param>
/// <param name="DueCount">Lessons that took place and are not paid.</param>
/// <param name="DueAmount">What they cost: the debt.</param>
/// <param name="UpcomingUnpaidCount">Unpaid lessons still to come.</param>
/// <param name="UpcomingUnpaidAmount">What they cost.</param>
/// <param name="LastReminderAt">The last payment reminder about this student, to them or a parent (UTC).</param>
/// <param name="Lessons">By date and time.</param>
public record StudentPaymentsDto(
    Guid? StudentId,
    string Name,
    string? Username,
    bool IsSubscribed,
    IReadOnlyList<string> Parents,
    int LessonCount,
    int PaidCount,
    decimal PaidAmount,
    int DueCount,
    decimal DueAmount,
    int UpcomingUnpaidCount,
    decimal UpcomingUnpaidAmount,
    DateTime? LastReminderAt,
    IReadOnlyList<PaymentLessonDto> Lessons);

/// <param name="LessonId">Lesson identifier.</param>
/// <param name="Date">School time.</param>
/// <param name="StartTime">School time.</param>
/// <param name="Teacher">Teacher name.</param>
/// <param name="TeacherColor">CSS hex color.</param>
/// <param name="Room">Room name.</param>
/// <param name="Note">The lesson note.</param>
/// <param name="Price">The lesson price.</param>
/// <param name="IsPaid">Marked as paid.</param>
/// <param name="PaidAt">When it was marked as paid (UTC).</param>
/// <param name="IsDue">Took place and is not paid.</param>
public record PaymentLessonDto(
    Guid LessonId,
    DateOnly Date,
    TimeOnly StartTime,
    string Teacher,
    string TeacherColor,
    string Room,
    string? Note,
    decimal Price,
    bool IsPaid,
    DateTime? PaidAt,
    bool IsDue);

/// <param name="LessonCount">Lessons in the period.</param>
/// <param name="PaidAmount">Paid.</param>
/// <param name="DueAmount">Took place and not paid.</param>
/// <param name="UpcomingUnpaidAmount">Still to come and not paid.</param>
/// <param name="DebtorCount">Students with a debt.</param>
public record PaymentTotalsDto(int LessonCount, decimal PaidAmount, decimal DueAmount, decimal UpcomingUnpaidAmount, int DebtorCount);
