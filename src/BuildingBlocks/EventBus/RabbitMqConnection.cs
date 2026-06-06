using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace BuildingBlocks.EventBus;

public sealed class RabbitMqConnection : IAsyncDisposable
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger<RabbitMqConnection> _logger;
    private readonly int _retryCount;
    private IConnection? _connection;
    private bool _disposed;
    public bool IsConnected => _connection is { IsOpen: true } &&  !_disposed;

    public RabbitMqConnection(IConnectionFactory connectionFactory, ILogger<RabbitMqConnection> logger, int retryCount = 3)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _retryCount = retryCount;
    }

    public async Task<IChannel> CreateChannelAsync()
    {
        if (!IsConnected)
        {
            await ConnectAsync();
        }

        return await _connection!.CreateChannelAsync();
    }

    public async Task ConnectAsync()
    {
        _logger.LogInformation("Setuping RabbitMQ connection...");
        var policy = Policy
            .Handle<BrokerUnreachableException>()
            .Or<Exception>()
            .WaitAndRetryAsync(
            _retryCount,
            attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            (ex, time) =>
            {
                _logger.LogWarning("RabbitMQ connection failed. Retrying in {Timeout}s...", time.TotalSeconds);
            });

        await policy.ExecuteAsync(async () =>
        {
            _connection = await _connectionFactory.CreateConnectionAsync();
            if (IsConnected)
            {
                _logger.LogInformation("RabbitMQ connection established: {Host}", _connectionFactory.Uri);
            }
        });

    }

    public async ValueTask DisposeAsync()
    {
        if(_disposed) return;
        _disposed = true;
        if(_connection is not null)
            await _connection.DisposeAsync();
    }
}
