using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Infrastructure.Persistence;

// Without at least one rule nothing is ever detected, and a system that silently detects nothing
// is worse than one that is obviously misconfigured. This writes a catch-all once, on an empty
// table, and never touches it again — so a tuned threshold is never overwritten on restart.
public sealed class DetectionRuleSeeder : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DetectionRuleSeeder> _logger;

    public DetectionRuleSeeder(
        IServiceScopeFactory scopeFactory,
        ILogger<DetectionRuleSeeder> logger
    )
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();

            var rules = scope.ServiceProvider.GetRequiredService<IDetectionRuleRepository>();

            if (await rules.CountAsync(cancellationToken) > 0)
                return;

            var rule = DetectionRule.Create(
                name: "Default error burst",
                service: null,
                minSeverity: LogSeverity.Error,
                windowSeconds: 300,
                threshold: 3,
                dedupWindowHours: 24,
                promoteThreshold: 0.90
            );

            await rules.AddAsync(rule, cancellationToken);
            await rules.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Seeded the default detection rule: {Threshold} occurrence(s) in {WindowSeconds}s, promote at {PromoteThreshold}.",
                rule.Threshold,
                rule.WindowSeconds,
                rule.PromoteThreshold
            );
        }
        catch (Exception ex)
        {
            // Seeding must not stop the service from starting; the missing rule is loud enough on
            // its own when detection reports having none.
            _logger.LogError(ex, "Failed to seed the default detection rule.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
