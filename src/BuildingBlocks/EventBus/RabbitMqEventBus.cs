using BuildingBlocks.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace BuildingBlocks.EventBus;

public sealed class RabbitMqEventBus : IEventBus, IAsyncDisposable
{
    private readonly RabbitMqConnection _connection;
    private readonly ILogger<RabbitMqEventBus> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EventBusOptions _options;

    // Event name (= routing key) -> deserialize + handle. Registered by Subscribe<T, THandler>(),
    // which captures T and THandler so dispatch needs no reflection.
    private readonly Dictionary<string, Func<string, IServiceProvider, CancellationToken, Task>> _handlers = new();

    private readonly SemaphoreSlim _consumerLock = new(1, 1);
    private IChannel? _consumerChannel;

    public RabbitMqEventBus(RabbitMqConnection connection, ILogger<RabbitMqEventBus> logger, IServiceScopeFactory scopeFactory, EventBusOptions options)
    {
        _connection = connection;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options;
    }

    // One durable queue per service. Two services subscribed to the same event each get their own
    // copy; a queue per event type would make them competing consumers instead.
    private string QueueName => _options.SubscriptionClientName;

    private string DeadLetterExchangeName => $"{_options.ExchangeName}.dlx";

    private string DeadLetterQueueName => $"{QueueName}.dlq";

    public async Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default) where T : IntegrationEvent
    {
        var eventName = integrationEvent.EventType;
        var body = JsonSerializer.SerializeToUtf8Bytes(integrationEvent);

        await using var channel = await _connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);

        var props = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            ContentType = "application/json",
            MessageId = integrationEvent.Id.ToString(),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await channel.BasicPublishAsync(
            exchange: _options.ExchangeName,
            routingKey: eventName,
            basicProperties: props,
            body: body,
            mandatory: true,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Published integration event: {EventName} with Id: {EventId}", eventName, integrationEvent.Id);
    }

    public void Subscribe<T, THandler>()
        where T : IntegrationEvent
        where THandler : IIntegrationEventHandler<T>
    {
        var eventName = typeof(T).Name;

        _handlers[eventName] = async (message, serviceProvider, cancellationToken) =>
        {
            var integrationEvent =
                JsonSerializer.Deserialize<T>(message)
                ?? throw new InvalidOperationException($"Deserialization returned null for event {eventName}.");

            // The one place a consumed message establishes who it is about. Here rather than in
            // each handler: a handler that sets its own scope has decided who it is working for,
            // and five handlers each remembering is five chances to forget.
            serviceProvider
                .GetRequiredService<IOrganizationContext>()
                .Set(integrationEvent.OrganizationId);

            var handler = serviceProvider.GetRequiredService<THandler>();

            await handler.HandleAsync(integrationEvent, cancellationToken);
        };

        _ = BindAsync(eventName);
    }

    // Binds the service queue to one more routing key, opening the shared consumer channel on the
    // first subscription. The lock keeps concurrent Subscribe calls from opening two channels.
    private async Task BindAsync(string eventName)
    {
        try
        {
            await _consumerLock.WaitAsync();
            try
            {
                _consumerChannel ??= await CreateConsumerChannelAsync();

                await _consumerChannel.QueueBindAsync(
                    queue: QueueName,
                    exchange: _options.ExchangeName,
                    routingKey: eventName);
            }
            finally
            {
                _consumerLock.Release();
            }

            _logger.LogInformation(
                "Bound queue {QueueName} to routing key {EventName}.", QueueName, eventName);
        }
        catch (Exception ex)
        {
            // Subscribe() is fire-and-forget, so an unlogged failure here would leave the service
            // running with no consumer and no visible reason why.
            _logger.LogError(ex, "Failed to subscribe to integration event: {EventName}", eventName);
        }
    }

    private async Task<IChannel> CreateConsumerChannelAsync()
    {
        if (string.IsNullOrWhiteSpace(QueueName))
        {
            throw new InvalidOperationException(
                $"{EventBusOptions.SectionName}:{nameof(EventBusOptions.SubscriptionClientName)} must be set — it is the service's queue name.");
        }

        var channel = await _connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Direct,
            durable: true);

        // Rejected messages land here instead of being redelivered forever.
        await channel.ExchangeDeclareAsync(
            exchange: DeadLetterExchangeName,
            type: ExchangeType.Direct,
            durable: true);

        await channel.QueueDeclareAsync(
            queue: DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        await channel.QueueBindAsync(
            queue: DeadLetterQueueName,
            exchange: DeadLetterExchangeName,
            routingKey: QueueName);

        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = DeadLetterExchangeName,
                ["x-dead-letter-routing-key"] = QueueName
            });

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) => await ProcessMessageAsync(channel, ea);

        await channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation(
            "Consuming integration events from queue {QueueName} (dead-letter queue {DeadLetterQueueName}).",
            QueueName,
            DeadLetterQueueName);

        return channel;
    }

    private async Task ProcessMessageAsync(IChannel channel, BasicDeliverEventArgs ea)
    {
        // The queue carries every event this service subscribed to, so the routing key decides
        // which handler runs.
        var eventName = ea.RoutingKey;

        if (!_handlers.TryGetValue(eventName, out var handler))
        {
            _logger.LogWarning(
                "No handler registered for integration event: {EventName}. Dead-lettering the message.", eventName);

            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        var message = Encoding.UTF8.GetString(ea.Body.Span);

        try
        {
            using var scope = _scopeFactory.CreateScope();

            await handler(message, scope.ServiceProvider, CancellationToken.None);

            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);

            _logger.LogInformation("Handled integration event: {EventName}", eventName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when handling integration event: {EventName}", eventName);

            // requeue: false — the broker dead-letters the message rather than looping it back to
            // this consumer, which would spin forever on a poison message.
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_consumerChannel is not null)
            await _consumerChannel.DisposeAsync();

        _consumerLock.Dispose();

        await _connection.DisposeAsync();
    }
}
