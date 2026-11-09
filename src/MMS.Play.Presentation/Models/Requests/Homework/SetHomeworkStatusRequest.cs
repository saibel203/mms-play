using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.Models.Homework;

namespace MMS.Play.Presentation.Models.Requests.Homework;

public class SetHomeworkStatusRequest
{
    /// <summary>
    /// Completed (no more reminders) or Active again.
    /// </summary>
    [Required]
    public HomeworkStatus? Status { get; set; }
}
