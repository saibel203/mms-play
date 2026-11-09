namespace MMS.Play.Contracts.Models.Homework;

public enum HomeworkStatus
{
    /// <summary>
    /// To practise: shown in the bot and on the practice page, reminded about every day.
    /// </summary>
    Active,

    /// <summary>
    /// Done (marked by an admin): kept for the history, no more reminders.
    /// </summary>
    Completed
}
