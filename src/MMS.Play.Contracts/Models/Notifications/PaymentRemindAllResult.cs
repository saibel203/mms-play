namespace MMS.Play.Contracts.Models.Notifications;

/// <summary>
/// What reminding everybody who owes did.
/// </summary>
/// <param name="Debtors">Students who owe for lessons that took place.</param>
/// <param name="NotReachable">Debtors nobody could be reminded about: neither the student nor a parent is subscribed with «Reminders» on.</param>
/// <param name="Sent">Messages delivered to Telegram (students and parents).</param>
/// <param name="Failed">Messages that could not be sent, the subscriber stays active.</param>
/// <param name="Unsubscribed">Subscribers who blocked the bot or whose chat no longer exists.</param>
public record PaymentRemindAllResult(int Debtors, int NotReachable, int Sent, int Failed, int Unsubscribed);
