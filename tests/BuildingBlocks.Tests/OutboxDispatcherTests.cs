using BuildingBlocks.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BuildingBlocks.Tests;

// The dispatcher's ordering is the reason at-least-once delivery works at all, and it is the kind
// of thing a well-meaning refactor reverses because the other order reads more naturally.
public sealed class OutboxDispatcherTests
{
    private static readonly TimeSpan CycleTimeout = TimeSpan.FromSeconds(10);

    private readonly IOutboxStore _store = Substitute.For<IOutboxStore>();
    private readonly List<IOutboxMessageHandler> _handlers = [];

    private readonly TaskCompletionSource _saved = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private readonly TaskCompletionSource _fetched = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    public OutboxDispatcherTests()
    {
        _store
            .When(store => store.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => _saved.TrySetResult());
    }

    private static OutboxMessage Message(string type = "SignalPromotedDomainEvent") =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            Payload = "{}",
            OccurredOn = DateTimeOffset.UtcNow,
            OrganizationId = Guid.NewGuid(),
        };

    private void GivenPending(params OutboxMessage[] messages)
    {
        IReadOnlyList<OutboxMessage> pending = messages;

        _store
            .GetPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _fetched.TrySetResult();
                return pending;
            });
    }

    // Runs the dispatcher until `until` completes, then stops it. Driving the real
    // BackgroundService rather than the private method keeps the scope handling and handler
    // resolution in the test.
    private async Task RunOneCycleAsync(Task until)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_store);

        foreach (var handler in _handlers)
            services.AddSingleton(handler);

        await using var provider = services.BuildServiceProvider();

        var dispatcher = new OutboxDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OutboxDispatcher>.Instance
        );

        await dispatcher.StartAsync(CancellationToken.None);

        try
        {
            await until.WaitAsync(CycleTimeout);
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task TheSideEffectHappensBeforeTheRowIsStamped()
    {
        // Load-bearing and easy to reverse. If the row were stamped first, a crash between the
        // two operations would mark a message delivered that never left the building — and
        // nothing would ever retry it. The other way round costs a duplicate, which the
        // consumers are idempotent against.
        var message = Message();
        GivenPending(message);

        var handler = new RecordingHandler(message.Type);
        _handlers.Add(handler);

        await RunOneCycleAsync(_saved.Task);

        Assert.True(handler.WasCalled);
        Assert.Null(handler.ProcessedOnAtHandleTime);
        Assert.NotNull(message.ProcessedOn);
        Assert.Null(message.Error);
    }

    [Fact]
    public async Task AHandlerThatThrowsLeavesTheRowPendingForTheNextTick()
    {
        var message = Message();
        GivenPending(message);
        _handlers.Add(new ThrowingHandler(message.Type, "broker unreachable"));

        await RunOneCycleAsync(_saved.Task);

        Assert.Null(message.ProcessedOn);
        Assert.Equal(1, message.RetryCount);
        Assert.Equal("broker unreachable", message.Error);
    }

    [Fact]
    public async Task AnUnregisteredTypeIsRecordedOnTheRowRatherThanCrashingTheLoop()
    {
        // A message type nobody handles is a deployment mistake, not a reason for the dispatcher
        // to stop delivering everything else.
        var message = Message("SomethingNobodyHandles");
        GivenPending(message);

        await RunOneCycleAsync(_saved.Task);

        Assert.Null(message.ProcessedOn);
        Assert.Equal(1, message.RetryCount);
        Assert.Contains("SomethingNobodyHandles", message.Error);
    }

    [Fact]
    public async Task OneFailingMessageDoesNotHoldUpTheRest()
    {
        var poisoned = Message("Poisoned");
        var healthy = Message("Healthy");
        GivenPending(poisoned, healthy);

        _handlers.Add(new ThrowingHandler("Poisoned", "bad payload"));
        _handlers.Add(new RecordingHandler("Healthy"));

        await RunOneCycleAsync(_saved.Task);

        Assert.Null(poisoned.ProcessedOn);
        Assert.NotNull(healthy.ProcessedOn);
    }

    [Fact]
    public async Task TheWholeBatchIsSavedInOneGo()
    {
        var first = Message();
        var second = Message();
        GivenPending(first, second);
        _handlers.Add(new RecordingHandler(first.Type));

        await RunOneCycleAsync(_saved.Task);

        await _store.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnEmptyBatchWritesNothing()
    {
        GivenPending();

        await RunOneCycleAsync(_fetched.Task);

        await _store.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---- test doubles ----------------------------------------------------------------------

    private sealed class RecordingHandler(string messageType) : IOutboxMessageHandler
    {
        public string MessageType { get; } = messageType;

        public bool WasCalled { get; private set; }

        // Captured during the call, which is the only moment the ordering is observable.
        public DateTimeOffset? ProcessedOnAtHandleTime { get; private set; }

        public Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            ProcessedOnAtHandleTime = message.ProcessedOn;

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler(string messageType, string error) : IOutboxMessageHandler
    {
        public string MessageType { get; } = messageType;

        public Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(error);
    }
}
