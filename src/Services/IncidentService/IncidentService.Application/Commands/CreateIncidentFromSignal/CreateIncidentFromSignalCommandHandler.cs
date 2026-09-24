using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using BuildingBlocks.SharedKernel;
using IncidentService.Application.Abstractions;
using IncidentService.Application.DTOs;
using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IncidentService.Application.Commands.CreateIncidentFromSignal;

public sealed class CreateIncidentFromSignalCommandHandler
    : IRequestHandler<CreateIncidentFromSignalCommand>
{
    private readonly IIncidentRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly IRealtimeNotifier _realtime;
    private readonly ILogger<CreateIncidentFromSignalCommandHandler> _logger;
    private readonly IOrganizationContext _organization;

    public CreateIncidentFromSignalCommandHandler(
        IIncidentRepository repository,
        IEventBus eventBus,
        IRealtimeNotifier realtime,
        ILogger<CreateIncidentFromSignalCommandHandler> logger,
        IOrganizationContext organization
    )
    {
        _repository = repository;
        _eventBus = eventBus;
        _realtime = realtime;
        _logger = logger;
        _organization = organization;
    }

    public async Task Handle(
        CreateIncidentFromSignalCommand request,
        CancellationToken cancellationToken
    )
    {
        // The promotion event is delivered at least once, so the second arrival must be a no-op
        // rather than a second incident.
        var existing = await _repository.GetByIdAsync(request.IncidentId, cancellationToken);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Incident {IncidentId} already exists; ignoring the redelivered promotion.",
                request.IncidentId
            );

            return;
        }

        var incident = Incident.Create(
            request.Title,
            request.Description,
            ParsePriority(request.Severity),
            IncidentSource.Telemetry,
            assignedTeam: null,
            id: request.IncidentId,
            detectedAt: request.DetectedAt
        );

        await _repository.AddAsync(incident, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        // From here the existing chain takes over unchanged: analysis, then notifications.
        await _eventBus.PublishAsync(
            new IncidentDetectedEvent
            {
                // Set by the bus from SignalPromotedEvent before this handler ran. There is no
                // HttpContext anywhere on this path — a poll became a signal became a promotion —
                // so the organisation the incident belongs to is whichever one owned the telemetry
                // source, carried the whole way on the messages.
                OrganizationId = _organization.Required,
                IncidentId = incident.Id,
                Title = incident.Title,
                Description = incident.Description,
                Severity = incident.Priority.ToString(),
                Source = incident.Source.ToString(),
                DetectedAt = incident.DetectedAt,
            },
            cancellationToken
        );

        // The redelivery guard above matters here too: a second arrival returns early and never
        // reaches this, so a duplicated promotion cannot make the same incident appear twice on
        // an open screen.
        await _realtime.IncidentCreatedAsync(
            IncidentDto.FromDomain(incident),
            cancellationToken
        );

        _logger.LogInformation(
            "Opened incident {IncidentId} from a telemetry signal, detected at {DetectedAt:u}.",
            incident.Id,
            request.DetectedAt
        );
    }

    // The severity telemetry suggests is only a starting point; the AI analysis that follows sets
    // the real priority.
    private static IncidentPriority ParsePriority(string severity)
    {
        return Enum.TryParse<IncidentPriority>(severity, ignoreCase: true, out var parsed)
            ? parsed
            : IncidentPriority.Medium;
    }
}
