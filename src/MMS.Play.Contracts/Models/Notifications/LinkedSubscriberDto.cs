namespace MMS.Play.Contracts.Models.Notifications;

/// <summary>
/// Another subscriber linked to this one, e.g. a child of a parent.
/// </summary>
/// <param name="Id">Subscriber identifier.</param>
/// <param name="Name">First and last name.</param>
public record LinkedSubscriberDto(Guid Id, string Name);
