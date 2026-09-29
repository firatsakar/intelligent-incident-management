using IncidentService.Domain.ValueObjects;

namespace IncidentService.Application.Abstractions;

/// <summary>
/// The person behind the request, as their token names them — for the activity trail,
/// never for deciding anything; policies do that. Both null on a path no person started: a bus
/// message, a telemetry promotion.
/// </summary>
public interface ICurrentUser
{
    Guid? Id { get; }

    string? Name { get; }
}

public static class CurrentUserExtensions
{
    public static ActivityActor AsActor(this ICurrentUser user) => ActivityActor.User(user.Id, user.Name);
}
