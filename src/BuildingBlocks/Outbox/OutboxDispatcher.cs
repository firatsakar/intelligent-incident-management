using System.Diagnostics;
using BuildingBlocks.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Outbox;

public sealed class OutboxDispatcher : BackgroundService
{
    /// <summary>Listened to by the platform's tracing setup, which names it without referencing this project.</summary>
    public const string ActivitySourceName = "BuildingBlocks.Outbox";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Poll loop must never die. Log and continue to the next tick.
                _logger.LogError(ex, "Outbox dispatch cycle failed. Retrying next tick.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

        var messages = await store.GetPendingAsync(BatchSize, cancellationToken);

        if (messages.Count == 0)
            return;

        foreach (var message in messages)
        {
            // Under the trace the row was written inside, so the publish below continues it. A
            // row written with nothing in flight, or before rows carried this, starts its own.
            ActivityContext.TryParse(message.TraceParent, traceState: null, out var writtenInside);

            using var activity = ActivitySource.StartActivity(
                $"outbox dispatch {message.Type}",
                ActivityKind.Internal,
                writtenInside
            );

            activity?.SetTag("outbox.message.id", message.Id);
            activity?.SetTag("outbox.message.type", message.Type);
            activity?.SetTag("outbox.retry_count", message.RetryCount);

            try
            {
                // A scope per message, carrying the organisation its row was stamped with. One
                // batch can hold several organisations' rows, and a handler that reads anything
                // through a query filter needs the one this message is about — without it the
                // read does not fail, it quietly matches nothing. That is how every analysis
                // since organisations arrived reached the bus and never reached the index: the
                // handler's read came back null, indexing threw, and the row was retried, and
                // re-published, every five seconds from then on.
                using var messageScope = _scopeFactory.CreateScope();

                // An empty id is a row from before rows had owners; leaving the scope unset keeps
                // the refusal where the organisation is read rather than inventing one here.
                if (message.OrganizationId != Guid.Empty)
                    messageScope
                        .ServiceProvider.GetRequiredService<IOrganizationContext>()
                        .Set(message.OrganizationId);

                var handler =
                    messageScope
                        .ServiceProvider.GetServices<IOutboxMessageHandler>()
                        .FirstOrDefault(candidate =>
                            string.Equals(candidate.MessageType, message.Type, StringComparison.Ordinal)
                        )
                    ?? throw new NotSupportedException(
                        $"No outbox handler registered for message type '{message.Type}'."
                    );

                // Ordering is deliberate and load-bearing: the side effect happens first, and
                // only then is the row stamped. Reversing it would lose a message on a crash
                // between the two.
                await handler.HandleAsync(message, cancellationToken);

                message.ProcessedOn = DateTimeOffset.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.Message;

                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                _logger.LogError(
                    ex,
                    "Failed to dispatch outbox message {MessageId} (type {Type}, attempt {RetryCount}).",
                    message.Id,
                    message.Type,
                    message.RetryCount
                );
            }
        }

        await store.SaveChangesAsync(cancellationToken);
    }
}
