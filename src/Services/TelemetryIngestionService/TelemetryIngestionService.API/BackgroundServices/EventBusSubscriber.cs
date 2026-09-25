using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;

namespace TelemetryIngestionService.API.BackgroundServices;

/// <summary>
/// The first thing this service listens for. It has published for three steps and consumed
/// nothing until now.
/// </summary>
public sealed class EventBusSubscriber : IHostedService
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<EventBusSubscriber> _logger;

    public EventBusSubscriber(IEventBus eventBus, ILogger<EventBusSubscriber> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "EventBusSubscriber starting — subscribing to integration events..."
        );

        _eventBus.Subscribe<
            OrganizationCreatedEvent,
            IIntegrationEventHandler<OrganizationCreatedEvent>
        >();

        _logger.LogInformation("Subscribed to OrganizationCreatedEvent.");

        _eventBus.Subscribe<
            IncidentResolvedEvent,
            IIntegrationEventHandler<IncidentResolvedEvent>
        >();

        _logger.LogInformation("Subscribed to IncidentResolvedEvent.");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("EventBusSubscriber stopping.");
        return Task.CompletedTask;
    }
}
