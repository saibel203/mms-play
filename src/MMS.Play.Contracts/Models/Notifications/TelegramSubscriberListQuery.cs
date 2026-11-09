namespace MMS.Play.Contracts.Models.Notifications;

/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page.</param>
/// <param name="Search">Matched against first name, last name and username, ignored when empty.</param>
/// <param name="IsSubscribed">Only subscribed (true) or unsubscribed (false) users, all when null.</param>
public record TelegramSubscriberListQuery(int Page, int PageSize, string? Search = null, bool? IsSubscribed = null);
