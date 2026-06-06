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
    private readonly Dictionary<string, Type> _handlers = new();
    private IChannel? _consumerChannel;

    public RabbitMqEventBus(RabbitMqConnection connection, ILogger<RabbitMqEventBus> logger, IServiceScopeFactory scopeFactory, EventBusOptions options)
    {
        _connection = connection;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options;
    }

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

        _logger.LogInformation($"Published integration event: {eventName} with Id: {integrationEvent.Id}");

    }

    public void Subscribe<T, THandler>()
        where T : IntegrationEvent
        where THandler : IIntegrationEventHandler<T>
    {
        var eventName = typeof(T).Name;
        _handlers[eventName] = typeof(THandler);
        _ = StartConsumeAsync<T>();
    }

    private async Task StartConsumeAsync<T>() where T : IntegrationEvent
    {
        var eventName = typeof(T).Name;
        _consumerChannel = await _connection.CreateChannelAsync();

        await _consumerChannel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Direct,
            durable: true);

        await _consumerChannel.QueueDeclareAsync(
            queue: eventName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        await _consumerChannel.QueueBindAsync(
            queue: eventName,
            exchange: _options.ExchangeName,
            routingKey: eventName);

        var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var message = Encoding.UTF8.GetString(ea.Body.Span);
            await ProcessMessageAsync<T>(message, ea.DeliveryTag);
        };

        await _consumerChannel.BasicConsumeAsync(
            queue: eventName,
            autoAck: false,
            consumer: consumer);
    }

    private async Task ProcessMessageAsync<T>(
        string message, ulong deliveryTag)
        where T : IntegrationEvent
    {
        var eventName = typeof(T).Name;

        try
        {
            var integrationEvent = JsonSerializer
                .Deserialize<T>(message)!;

            using var scope = _scopeFactory.CreateScope();
            var handlerType = _handlers[eventName];
            var handler = scope.ServiceProvider
                .GetRequiredService(handlerType)
                as IIntegrationEventHandler<T>;

            await handler!.HandleAsync(integrationEvent);

            await _consumerChannel!.BasicAckAsync(deliveryTag, false);

            _logger.LogInformation(
                "Handled integration event: {EventName}", eventName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error when handle integration event: {EventName}", eventName);

            await _consumerChannel!.BasicNackAsync(
                deliveryTag, false, requeue: true);
        }
    }
    public async ValueTask DisposeAsync()
    {
        if (_consumerChannel is not null)
            await _consumerChannel.DisposeAsync();
        await _connection.DisposeAsync();
    }


}
