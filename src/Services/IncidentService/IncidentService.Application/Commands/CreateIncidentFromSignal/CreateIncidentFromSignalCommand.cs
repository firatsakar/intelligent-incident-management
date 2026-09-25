using MediatR;

namespace IncidentService.Application.Commands.CreateIncidentFromSignal;

public sealed record CreateIncidentFromSignalCommand : IRequest
{
    // Chosen by the telemetry service, not here. A redelivered promotion then collides on the
    // primary key instead of opening a second incident for the same signature.
    public required Guid IncidentId { get; init; }

    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Severity { get; init; }
    public required DateTime DetectedAt { get; init; }

    // Not stored on the incident: it is passed on to the analysis, which reads the service's code.
    public string? Service { get; init; }
}
