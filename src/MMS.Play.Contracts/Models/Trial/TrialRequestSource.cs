namespace MMS.Play.Contracts.Models.Trial;

/// <summary>
/// Where a trial lesson request came from.
/// </summary>
public enum TrialRequestSource
{
    /// <summary>
    /// The «🎸 Пробний урок» button in the bot; the request knows the subscriber.
    /// </summary>
    Bot,

    /// <summary>
    /// The public page /trial; only the name and the contact are known.
    /// </summary>
    Website
}
