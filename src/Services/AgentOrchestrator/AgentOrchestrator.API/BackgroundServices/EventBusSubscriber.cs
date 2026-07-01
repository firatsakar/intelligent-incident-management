using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;

namespace AgentOrchestrator.API.BackgroundServices;

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
            IncidentDetectedEvent,
            IIntegrationEventHandler<IncidentDetectedEvent>
        >();

        _logger.LogInformation("Subscribed to IncidentDetectedEvent.");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("EventBusSubscriber stopping.");
        return Task.CompletedTask;
    }
}
