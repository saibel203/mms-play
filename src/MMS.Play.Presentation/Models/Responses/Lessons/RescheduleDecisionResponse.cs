using MMS.Play.Contracts.Models.Lessons;

namespace MMS.Play.Presentation.Models.Responses.Lessons;

/// <param name="Decision">Approved, Declined, or SlotTaken when the time got taken and the request was declined instead.</param>
public record RescheduleDecisionResponse(RescheduleDecision Decision);
