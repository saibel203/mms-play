using MMS.Play.Contracts.Models.Homework;

namespace MMS.Play.Presentation.Models.Requests.Homework;

public class ListHomeworkRequest
{
    /// <summary>
    /// Only this student's homework.
    /// </summary>
    public Guid? StudentId { get; set; }

    /// <summary>
    /// Active or Completed; empty = both.
    /// </summary>
    public HomeworkStatus? Status { get; set; }

    public HomeworkListQuery ToQuery() => new(StudentId, Status);
}
