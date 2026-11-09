namespace MMS.Play.Contracts.Models.Notifications;

/// <param name="Sent">Messages delivered to Telegram.</param>
/// <param name="Failed">Messages that could not be sent, the subscriber stays active and gets the next reminder.</param>
/// <param name="Unsubscribed">Subscribers who blocked the bot or whose chat no longer exists.</param>
public record LessonReminderResult(int Sent, int Failed, int Unsubscribed);
