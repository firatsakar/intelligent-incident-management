using IncidentService.Application.DTOs;
using IncidentService.Domain.Aggregates;

namespace IncidentService.Application.Abstractions;

// Lets a handler announce a change to whoever is watching, without the Application layer knowing
// that SignalR exists. Same seam as INotificationChannel and ITelemetrySourceConnector: the
// interface lives here, the transport lives in the API layer.
//
// IncidentChanged carries the whole DTO. A socket exists to remove HTTP round trips, so pushing
// an id and making the client fetch would leave it doing one request per change — worse than
// polling, which at least coalesces. It is the same DTO the HTTP endpoint returns, so there is no
// second contract to keep in step.
//
// IncidentCreated used to carry an id alone, on the reasoning that whether a new incident belongs
// on the first page of a filtered, sorted list is a question only the server can answer. That part
// still holds and the client still re-reads the list — but the DTO was free at both call sites
// (one of them maps it on the very next line for its HTTP response), and sending it lets the
// client fill the detail cache at the same time. Opening the incident that just arrived then
// costs nothing.
public interface IRealtimeNotifier
{
    Task IncidentCreatedAsync(IncidentDto incident, CancellationToken cancellationToken = default);

    Task IncidentChangedAsync(IncidentDto incident, CancellationToken cancellationToken = default);

    /// <summary>
    /// A row added to an incident's activity trail. Its own message rather than riding on
    /// <see cref="IncidentChangedAsync"/>, because a comment does not change the incident.
    /// </summary>
    Task ActivityRecordedAsync(IncidentActivityDto activity, CancellationToken cancellationToken = default);
}

public static class RealtimeNotifierExtensions
{
    /// <summary>Announces the row a handler recorded, if it recorded one.</summary>
    public static Task ActivityRecordedAsync(
        this IRealtimeNotifier realtime,
        IncidentActivity? activity,
        CancellationToken cancellationToken = default
    ) =>
        activity is null
            ? Task.CompletedTask
            : realtime.ActivityRecordedAsync(IncidentActivityDto.FromDomain(activity), cancellationToken);
}
