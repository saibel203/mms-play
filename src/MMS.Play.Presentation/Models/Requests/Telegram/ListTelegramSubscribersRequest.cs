using System.ComponentModel.DataAnnotations;
using MMS.Play.Contracts.Models.Notifications;

namespace MMS.Play.Presentation.Models.Requests.Telegram;

/// <summary>
/// Query string of the Telegram subscriber list.
/// </summary>
public class ListTelegramSubscribersRequest
{
    public const int MaxPageSize = 100;

    /// <summary>
    /// 1-based page number.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>
    /// Items per page, at most 100.
    /// </summary>
    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Part of the first name, last name or username (with or without "@"), case insensitive.
    /// </summary>
    [StringLength(100)]
    public string? Search { get; set; }

    /// <summary>
    /// true: only users subscribed in the bot, false: only those who unsubscribed or blocked it. All when omitted.
    /// </summary>
    public bool? IsSubscribed { get; set; }

    public TelegramSubscriberListQuery ToQuery() => new(Page, PageSize, Search, IsSubscribed);
}
