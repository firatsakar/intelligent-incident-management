using NotificationService.Application.Abstractions;
using NotificationService.Application.Queries.GetNotificationStats;
using NotificationService.Domain.Aggregates;
using NotificationService.Domain.Enums;
using NSubstitute;

namespace NotificationService.Tests;

// Delivery health per channel. Until this existed, a channel that had been failing quietly for a
// day was invisible unless somebody opened the right incident — which defeats the purpose of
// having sent a notification at all.
public sealed class GetNotificationStatsQueryHandlerTests
{
    private static readonly DateTime Noon = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly INotificationDeliveryRepository _deliveries =
        Substitute.For<INotificationDeliveryRepository>();
    private readonly IIntegrationRepository _integrations =
        Substitute.For<IIntegrationRepository>();
    private readonly GetNotificationStatsQueryHandler _handler;

    public GetNotificationStatsQueryHandlerTests()
    {
        _handler = new GetNotificationStatsQueryHandler(_deliveries, _integrations);

        GivenDeliveries();
        GivenIntegrations();
    }

    private void GivenDeliveries(params NotificationDelivery[] rows)
    {
        IReadOnlyList<NotificationDelivery> result = rows;

        _deliveries
            .GetWindowAsync(
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(result);
    }

    private void GivenIntegrations(params Integration[] integrations)
    {
        IReadOnlyList<Integration> result = integrations;

        _integrations.GetAllAsync(Arg.Any<CancellationToken>()).Returns(result);
    }

    private static Integration EmailIntegration(string name = "Ops mailbox") =>
        Integration.Create(
            name,
            NotificationChannelType.Email,
            new Dictionary<string, string>
            {
                ["Host"] = "localhost",
                ["Port"] = "1025",
                ["From"] = "incidents@example.com",
                ["To"] = "oncall@example.com",
            },
            minPriority: null,
            categoryFilter: null
        );

    private static NotificationDelivery Sent(Guid integrationId)
    {
        var delivery = NotificationDelivery.Start(integrationId, Guid.NewGuid(), Guid.NewGuid());
        delivery.MarkSent();
        return delivery;
    }

    private static NotificationDelivery Failed(Guid integrationId, string error)
    {
        var delivery = NotificationDelivery.Start(integrationId, Guid.NewGuid(), Guid.NewGuid());
        delivery.MarkFailed(error);
        return delivery;
    }

    [Fact]
    public async Task CountsSplitByOutcome()
    {
        var integration = EmailIntegration();
        GivenIntegrations(integration);
        GivenDeliveries(
            Sent(integration.Id),
            Sent(integration.Id),
            Failed(integration.Id, "connection refused"),
            NotificationDelivery.Start(integration.Id, Guid.NewGuid(), Guid.NewGuid())
        );

        var stats = await _handler.Handle(
            new GetNotificationStatsQuery(),
            CancellationToken.None
        );

        Assert.Equal(2, stats.TotalSent);
        Assert.Equal(1, stats.TotalFailed);
        Assert.Equal(1, stats.TotalPending);

        var row = Assert.Single(stats.Integrations);
        Assert.Equal("Ops mailbox", row.Name);
        Assert.Equal("Email", row.Channel);
    }

    [Fact]
    public async Task ADeletedIntegrationStillReportsWhatItDid()
    {
        // The deliveries are the record that somebody was told, and the backend keeps them on
        // purpose when the integration goes. Grouping by the configured integrations instead
        // would erase that history from the one screen that exists to show it.
        GivenIntegrations();
        GivenDeliveries(Failed(Guid.NewGuid(), "410 Gone"));

        var row = Assert.Single(
            (await _handler.Handle(new GetNotificationStatsQuery(), CancellationToken.None))
                .Integrations
        );

        Assert.Null(row.Name);
        Assert.Null(row.Channel);
        Assert.False(row.IsEnabled);
        Assert.Equal(1, row.Failed);
        Assert.Equal("410 Gone", row.LastError);
    }

    [Fact]
    public async Task NothingDeliveredMeansNoDispatchTime_NotZero()
    {
        // Zero would read as "delivered instantly", which is the opposite of what happened.
        var integration = EmailIntegration();
        GivenIntegrations(integration);
        GivenDeliveries(Failed(integration.Id, "connection refused"));

        var row = Assert.Single(
            (await _handler.Handle(new GetNotificationStatsQuery(), CancellationToken.None))
                .Integrations
        );

        Assert.Null(row.MedianDispatchSeconds);
        Assert.Null(row.LastSentAt);
    }

    [Fact]
    public async Task TheLastErrorIsTheMostRecentOne()
    {
        // The only actionable thing on the row. Showing the first failure would describe a
        // problem that may well already be fixed.
        var integration = EmailIntegration();
        GivenIntegrations(integration);

        var older = Failed(integration.Id, "first failure");
        var newer = Failed(integration.Id, "most recent failure");

        GivenDeliveries(older, newer);

        var row = Assert.Single(
            (await _handler.Handle(new GetNotificationStatsQuery(), CancellationToken.None))
                .Integrations
        );

        Assert.Equal(2, row.Failed);
        Assert.Contains(
            row.LastError,
            new[] { "first failure", "most recent failure" }
        );
        Assert.NotNull(row.LastErrorAt);
    }

    [Fact]
    public async Task TheWorstChannelIsListedFirst()
    {
        // An operator opening this screen is looking for what is broken, so the ordering does the
        // first pass of that work rather than leaving it to the eye.
        var healthy = EmailIntegration("Healthy");
        var broken = EmailIntegration("Broken");

        GivenIntegrations(healthy, broken);
        GivenDeliveries(
            Sent(healthy.Id),
            Failed(broken.Id, "connection refused"),
            Failed(broken.Id, "connection refused")
        );

        var stats = await _handler.Handle(
            new GetNotificationStatsQuery(),
            CancellationToken.None
        );

        Assert.Equal("Broken", stats.Integrations[0].Name);
    }

    [Fact]
    public async Task AnOverwideWindowIsClamped()
    {
        await _handler.Handle(
            new GetNotificationStatsQuery(Noon.AddYears(-2), Noon),
            CancellationToken.None
        );

        await _deliveries
            .Received(1)
            .GetWindowAsync(
                Noon - GetNotificationStatsQueryHandler.MaxWindow,
                Noon,
                Arg.Any<CancellationToken>()
            );
    }
}
