using BuildingBlocks.Contracts;
using BuildingBlocks.EventBus;
using Microsoft.Extensions.Logging;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Application.EventHandlers;

/// <summary>
/// Gives a new organisation the detection rule it cannot work without.
/// </summary>
/// <remarks>
/// The reason <c>OrganizationCreatedEvent</c> exists. The service that has to write the rule is
/// this one; the place an organisation is born is IdentityService; and they have separate
/// databases, so there is no query that could have found this out. Without the rule an
/// organisation ingests logs, builds signatures and detects nothing — and detecting nothing is
/// indistinguishable from a quiet week.
/// </remarks>
public sealed class OrganizationCreatedEventHandler
    : IIntegrationEventHandler<OrganizationCreatedEvent>
{
    private readonly IDetectionRuleRepository _rules;
    private readonly ILogger<OrganizationCreatedEventHandler> _logger;

    public OrganizationCreatedEventHandler(
        IDetectionRuleRepository rules,
        ILogger<OrganizationCreatedEventHandler> logger
    )
    {
        _rules = rules;
        _logger = logger;
    }

    public async Task HandleAsync(
        OrganizationCreatedEvent integrationEvent,
        CancellationToken cancellationToken = default
    )
    {
        // At-least-once delivery, so this can arrive twice. Counting first rather than catching a
        // unique violation: a second arrival should be a no-op, and it must not overwrite a
        // threshold somebody has since tuned. The count is scoped by the query filter, using the
        // organisation the bus established from this very event.
        if (await _rules.CountAsync(cancellationToken) > 0)
            return;

        var rule = DefaultDetectionRule.For(integrationEvent.OrganizationId);

        await _rules.AddAsync(rule, cancellationToken);
        await _rules.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Wrote the default detection rule for organisation {OrganizationId}: "
                + "{Threshold} occurrence(s) in {WindowSeconds}s, promote at {PromoteThreshold}.",
            integrationEvent.OrganizationId,
            rule.Threshold,
            rule.WindowSeconds,
            rule.PromoteThreshold
        );
    }
}
