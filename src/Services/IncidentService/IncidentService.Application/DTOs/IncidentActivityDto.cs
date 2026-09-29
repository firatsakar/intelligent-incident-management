using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Enums;

namespace IncidentService.Application.DTOs;

public sealed record IncidentActivityDto
{
    public Guid Id { get; init; }
    public Guid IncidentId { get; init; }
    public IncidentActivityKind Kind { get; init; }
    public DateTime At { get; init; }
    public ActivityActorKind ActorKind { get; init; }
    public Guid? ActorId { get; init; }
    public string? ActorName { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public IncidentVerdict? Verdict { get; init; }
    public string? Text { get; init; }

    public static IncidentActivityDto FromDomain(IncidentActivity activity) =>
        new()
        {
            Id = activity.Id,
            IncidentId = activity.IncidentId,
            Kind = activity.Kind,
            At = activity.CreatedAt,
            ActorKind = activity.ActorKind,
            ActorId = activity.ActorId,
            ActorName = activity.ActorName,
            From = activity.From,
            To = activity.To,
            Verdict = activity.Verdict,
            Text = activity.Text,
        };
}
