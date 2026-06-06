namespace BuildingBlocks.EventBus;

public interface IIntegrationEvent
{
    Guid Id { get; }
    DateTime OccurredAt { get; }
    string EventType { get; }
}

public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => GetType().Name;
}

public interface IEventBus
{
    Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default) where T : IntegrationEvent;
    void Subscribe<T, THandler>()
        where T : IntegrationEvent
        where THandler : IIntegrationEventHandler<T>;
}

public interface IIntegrationEventHandler<in TEvent> where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken = default);
}