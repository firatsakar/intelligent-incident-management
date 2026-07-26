using System.Text.Json;
using AgentOrchestrator.Application.Abstractions;
using AgentOrchestrator.Domain.Events;
using AgentOrchestrator.Infrastructure.Persistence;
using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentOrchestrator.Infrastructure.Outbox;

public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcher> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;

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
        var db = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        var indexer = scope.ServiceProvider.GetRequiredService<IAnalysisIndexer>();
        var repository = scope.ServiceProvider.GetRequiredService<IIncidentAnalysisRepository>();

        var messages = await db
            .OutboxMessages.Where(m => m.ProcessedOn == null)
            .OrderBy(m => m.OccurredOn)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
            return;

        foreach (var message in messages)
        {
            try
            {
                await DispatchAsync(message, eventBus, indexer, repository, cancellationToken);

                message.ProcessedOn = DateTimeOffset.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.Message;

                _logger.LogError(
                    ex,
                    "Failed to dispatch outbox message {MessageId} (type {Type}, attempt {RetryCount}).",
                    message.Id,
                    message.Type,
                    message.RetryCount
                );
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task DispatchAsync(
        OutboxMessage message,
        IEventBus eventBus,
        IAnalysisIndexer indexer,
        IIncidentAnalysisRepository repository,
        CancellationToken cancellationToken
    )
    {
        switch (message.Type)
        {
            case nameof(IncidentAnalysisCompletedDomainEvent):
                var domainEvent =
                    JsonSerializer.Deserialize<IncidentAnalysisCompletedDomainEvent>(
                        message.Payload
                    )
                    ?? throw new InvalidOperationException(
                        $"Failed to deserialize payload for message {message.Id}."
                    );

                var integrationEvent = new IncidentAnalyzedEvent
                {
                    IncidentId = domainEvent.IncidentId,
                    SuggestedCategory = domainEvent.SuggestedCategory,
                    SuggestedPriority = domainEvent.SuggestedPriority,
                    Reasoning = domainEvent.Reasoning,
                };

                await eventBus.PublishAsync(integrationEvent, cancellationToken);

                var analysis = await repository.GetByIncidentIdAsync(
                    domainEvent.IncidentId,
                    cancellationToken
                );

                await indexer.IndexAsync(analysis, cancellationToken);

                break;

            default:
                throw new NotSupportedException($"Unknown outbox message type: {message.Type}");
        }
    }
}
