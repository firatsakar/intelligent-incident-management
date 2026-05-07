namespace BuildingBlocks.EventBus;

internal interface IEventBus
{
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default);
    Task SubscribeAsync<T, THandler>() where THandler : IEventHandler<T>;
}

public interface IEventHandler<in T>
{
    Task Handler(T @evet, CancellationToken cancellationToken = default);
}