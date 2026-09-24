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

    /// <summary>
    /// Whose data this event is about.
    /// </summary>
    /// <remarks>
    /// Inside the message rather than beside it, because there is no beside: BasicProperties here
    /// carries a delivery mode, a content type, an id and a timestamp, and no headers dictionary.
    /// Adding one would make the organisation something a consumer could forget to read.
    ///
    /// <c>required</c> on purpose. Seven of this platform's consumers have no HttpContext to take
    /// a scope from, so a message that arrives without one leaves a handler either guessing or
    /// writing a row nobody owns. Making it a compile error at every construction site is the only
    /// version of this that cannot be forgotten once.
    /// </remarks>
    public required Guid OrganizationId { get; init; }
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