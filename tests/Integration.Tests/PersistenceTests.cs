using AgentOrchestrator.Domain.Aggregates;
using AgentOrchestrator.Domain.ValueObjects;
using AgentOrchestrator.Infrastructure.Persistence;
using BuildingBlocks.Outbox;
using BuildingBlocks.Web;
using IncidentService.Application.Abstractions;
using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Enums;
using IncidentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotificationService.Domain.Aggregates;
using NotificationService.Infrastructure.Persistence;
using Npgsql;
using NSubstitute;
using IncidentPriority = IncidentService.Domain.Enums.IncidentPriority;
using NotificationChannelType = NotificationService.Domain.Enums.NotificationChannelType;
using NotificationIntegration = NotificationService.Domain.Aggregates.Integration;

namespace Integration.Tests;

// What only a real database can say (Adım 23): that a unique index is there and does what the
// code relies on it for, that a query filter is real SQL and not a hope, and that what is written
// can be read back.
[Collection(PostgresCollection.Name)]
public sealed class PersistenceTests(PostgresFixture postgres)
{
    private static readonly Guid OrgA = Guid.NewGuid();
    private static readonly Guid OrgB = Guid.NewGuid();

    private async Task<ServiceProvider> Migrated<TContext>(
        string connectionName,
        Action<IServiceCollection, Microsoft.Extensions.Configuration.IConfiguration> addInfrastructure
    )
        where TContext : DbContext
    {
        var provider = ServiceUnderTest.Build(connectionName, await postgres.NewDatabaseAsync(), addInfrastructure);

        var host = Substitute.For<IHost>();
        host.Services.Returns(provider);
        await host.MigrateOnStartupAsync<TContext>();

        return provider;
    }

    private Task<ServiceProvider> IncidentService() =>
        Migrated<IncidentDbContext>(
            "IncidentDb",
            (services, configuration) => global::IncidentService.Infrastructure.ServiceCollectionExtensions.AddInfrastructure(services, configuration)
        );

    private static Incident Alert(string externalId) =>
        Incident.Create(OrgA, "Checkout error rate above 5%", "From alerting.", IncidentPriority.High, IncidentSource.Alert, externalId: externalId, reportedBy: "Grafana");

    // ---- organisation filters -----------------------------------------------------------------

    [Fact]
    public async Task AnotherOrganisationsIncidentsAndKeysAreNotThere()
    {
        await using var provider = await IncidentService();

        Guid incidentId;
        await using (var a = provider.ScopeFor(OrgA))
        {
            var incidents = a.ServiceProvider.GetRequiredService<IIncidentRepository>();
            var incident = Incident.Create(OrgA, "Only A's", "Only A's.", IncidentPriority.Low, IncidentSource.Manual);
            await incidents.AddAsync(incident);
            await incidents.SaveChangesAsync();
            incidentId = incident.Id;

            var keys = a.ServiceProvider.GetRequiredService<IIncidentApiKeyRepository>();
            await keys.AddAsync(IncidentApiKey.Issue(OrgA, "A's key", "Admin").Key);
            await keys.SaveChangesAsync();
        }

        await using (var b = provider.ScopeFor(OrgB))
        {
            Assert.Null(await b.ServiceProvider.GetRequiredService<IIncidentRepository>().GetByIdAsync(incidentId));
            Assert.Empty(await b.ServiceProvider.GetRequiredService<IIncidentApiKeyRepository>().ListAsync());
            Assert.Equal(0, await b.ServiceProvider.GetRequiredService<IncidentDbContext>().Incidents.CountAsync());
        }

        await using (var a = provider.ScopeFor(OrgA))
        {
            Assert.NotNull(await a.ServiceProvider.GetRequiredService<IIncidentRepository>().GetByIdAsync(incidentId));
            Assert.Single(await a.ServiceProvider.GetRequiredService<IIncidentApiKeyRepository>().ListAsync());
        }
    }

    // ---- Adım 27: one open incident per external id --------------------------------------------

    [Fact]
    public async Task ASecondOpenIncidentWithTheSameExternalIdIsRefusedAndLeavesNothingBehind()
    {
        await using var provider = await IncidentService();
        await using var scope = provider.ScopeFor(OrgA);
        var incidents = scope.ServiceProvider.GetRequiredService<IIncidentRepository>();

        await incidents.AddAsync(Alert("alert-1"));
        await incidents.SaveChangesAsync();

        await incidents.AddAsync(Alert("alert-1"));
        await Assert.ThrowsAsync<DuplicateExternalIdException>(() => incidents.SaveChangesAsync());

        // The refused insert is out of the unit of work; the next save is not the same failure.
        await incidents.SaveChangesAsync();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IncidentDbContext>().Incidents.CountAsync());
    }

    [Fact]
    public async Task OnceResolvedTheSameExternalIdOpensANewIncident()
    {
        await using var provider = await IncidentService();
        await using var scope = provider.ScopeFor(OrgA);
        var incidents = scope.ServiceProvider.GetRequiredService<IIncidentRepository>();

        var first = Alert("alert-2");
        await incidents.AddAsync(first);
        await incidents.SaveChangesAsync();

        first.UpdateStatus(IncidentStatus.Resolved, IncidentVerdict.Real);
        await incidents.SaveChangesAsync();

        var recurrence = Alert("alert-2");
        await incidents.AddAsync(recurrence);
        await incidents.SaveChangesAsync();

        Assert.Equal(recurrence.Id, (await incidents.GetOpenByExternalIdAsync("alert-2"))?.Id);
    }

    [Fact]
    public async Task OtherOrganisationsMayUseTheSameExternalId()
    {
        await using var provider = await IncidentService();

        await using (var a = provider.ScopeFor(OrgA))
        {
            var incidents = a.ServiceProvider.GetRequiredService<IIncidentRepository>();
            await incidents.AddAsync(Alert("shared-name"));
            await incidents.SaveChangesAsync();
        }

        await using (var b = provider.ScopeFor(OrgB))
        {
            var incidents = b.ServiceProvider.GetRequiredService<IIncidentRepository>();
            await incidents.AddAsync(
                Incident.Create(OrgB, "B's", "B's.", IncidentPriority.Low, IncidentSource.Alert, externalId: "shared-name")
            );
            await incidents.SaveChangesAsync();
        }
    }

    // ---- notifications: one delivery per integration and incident -------------------------------

    [Fact]
    public async Task ASecondDeliveryRecordForTheSameIntegrationAndIncidentIsRefused()
    {
        await using var provider = await Migrated<NotificationDbContext>(
            "NotificationDb",
            (services, configuration) => global::NotificationService.Infrastructure.ServiceCollectionExtensions.AddInfrastructure(services, configuration)
        );

        var incidentId = Guid.NewGuid();
        Guid integrationId;

        await using (var scope = provider.ScopeFor(OrgA))
        {
            var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var integration = NotificationIntegration.Create(
                OrgA,
                "On-call webhook",
                NotificationChannelType.Webhook,
                new Dictionary<string, string> { ["Url"] = "https://example.com/hook" }
            );
            context.Integrations.Add(integration);
            context.NotificationDeliveries.Add(NotificationDelivery.Start(OrgA, integration.Id, incidentId, Guid.NewGuid()));
            await context.SaveChangesAsync();
            integrationId = integration.Id;
        }

        // A redelivered event racing the first: its check came back empty, its insert must not land.
        await using (var scope = provider.ScopeFor(OrgA))
        {
            var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            context.NotificationDeliveries.Add(NotificationDelivery.Start(OrgA, integrationId, incidentId, Guid.NewGuid()));

            var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, (error.InnerException as PostgresException)?.SqlState);
        }
    }

    // ---- Adım 17.5: an analysis with suspected changes reads back ----------------------------

    [Fact]
    public async Task AnAnalysisWithRelatedChangesIsReadBackAndCanBeReplaced()
    {
        await using var provider = await Migrated<AgentDbContext>(
            "AgentDb",
            (services, configuration) => global::AgentOrchestrator.Infrastructure.ServiceCollectionExtensions.AddInfrastructure(services, configuration)
        );

        static RelatedChange Change(string sha) =>
            new()
            {
                Sha = sha,
                Title = "Lower the payment gateway timeout",
                Author = "dev",
                CommittedAt = new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc),
                Url = $"https://github.com/acme/shop/commit/{sha}",
            };

        static AnalysisResult Result(params RelatedChange[] changes) =>
            new()
            {
                SuggestedPriority = "High",
                SuggestedCategory = "Application",
                Reasoning = "The timeout was lowered in the deploy before the errors began.",
                SuggestedSteps = ["Revert the timeout change", "Watch the error rate"],
                Confidence = 0.8,
                RelatedChanges = changes.ToList(),
            };

        Guid analysisId;
        await using (var scope = provider.ScopeFor(OrgA))
        {
            var context = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
            var analysis = IncidentAnalysis.Create(OrgA, Guid.NewGuid(), "Checkout 502s", "Timeouts.");
            context.Analyses.Add(analysis);
            await context.SaveChangesAsync();

            analysis.MarkAsCompleted(Result(Change("6e2528c1")));
            await context.SaveChangesAsync();
            analysisId = analysis.Id;
        }

        // Read in a scope of its own: materialised from the database, not the change tracker. The
        // Adım 17.5 bug was exactly here — a fixed-size default that EF could not fill.
        await using (var scope = provider.ScopeFor(OrgA))
        {
            var context = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
            var loaded = await context.Analyses.SingleAsync(x => x.Id == analysisId);

            Assert.Equal("6e2528c1", Assert.Single(loaded.Result!.RelatedChanges).Sha);
            Assert.Equal(2, loaded.Result.SuggestedSteps.Count);

            loaded.MarkAsCompleted(Result(Change("aaaa1111"), Change("bbbb2222")));
            await context.SaveChangesAsync();
        }

        await using (var scope = provider.ScopeFor(OrgA))
        {
            var loaded = await scope.ServiceProvider.GetRequiredService<AgentDbContext>().Analyses.SingleAsync(x => x.Id == analysisId);
            Assert.Equal(["aaaa1111", "bbbb2222"], loaded.Result!.RelatedChanges.Select(change => change.Sha));
        }
    }

    // ---- Adım 28: what the dispatcher picks, in real SQL --------------------------------------

    [Fact]
    public async Task TheDispatcherSkipsWhatIsDoneParkedOrStillWaiting()
    {
        await using var provider = await IncidentService();
        var now = DateTimeOffset.UtcNow;

        OutboxMessage Row(int minutesAgo) =>
            new()
            {
                Id = Guid.NewGuid(),
                OrganizationId = OrgA,
                Type = "IncidentResolvedDomainEvent",
                Payload = "{}",
                OccurredOn = now.AddMinutes(-minutesAgo),
            };

        var fresh = Row(5);
        var ready = Row(4);
        ready.MarkFailed("broker unreachable", now.AddMinutes(-3));
        var waiting = Row(3);
        waiting.MarkFailed("broker unreachable", now);
        var done = Row(2);
        done.MarkDispatched(now);
        var parked = Row(1);
        parked.ParkedAt = now;

        await using (var scope = provider.ScopeFor(OrgA))
        {
            var context = scope.ServiceProvider.GetRequiredService<IncidentDbContext>();
            context.OutboxMessages.AddRange(fresh, ready, waiting, done, parked);
            await context.SaveChangesAsync();
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

            Assert.Equal([fresh.Id, ready.Id], (await store.GetPendingAsync(20)).Select(row => row.Id));
            Assert.Equal(1, await store.CountParkedAsync());
        }
    }
}
