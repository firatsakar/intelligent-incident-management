using IncidentService.Domain.Enums;

namespace IncidentService.Domain.ValueObjects;

/// <summary>
/// Who an activity row names. A person carries their id and the name they had then; a key carries
/// its name; the detector and the analysis are nobody in particular.
/// </summary>
public sealed record ActivityActor(ActivityActorKind Kind, Guid? Id = null, string? Name = null)
{
    public static ActivityActor User(Guid? id, string? name) => new(ActivityActorKind.User, id, name);

    public static ActivityActor ApiKey(string name) => new(ActivityActorKind.ApiKey, Name: name);

    public static ActivityActor Detector { get; } = new(ActivityActorKind.Detector);

    public static ActivityActor Ai { get; } = new(ActivityActorKind.Ai);
}
