using IncidentService.Application.DTOs;

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
// IncidentCreated carries an id alone, deliberately. Whether a new incident belongs on the first
// page of a filtered, sorted list is a question only the server can answer; guessing client-side
// produces a list that disagrees with the server as soon as the user pages.
public interface IRealtimeNotifier
{
    Task IncidentCreatedAsync(Guid incidentId, CancellationToken cancellationToken = default);

    Task IncidentChangedAsync(IncidentDto incident, CancellationToken cancellationToken = default);
}
