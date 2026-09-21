using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Commands.DispatchNotifications;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Aggregates;
using NotificationService.Domain.Enums;

namespace NotificationService.Tests;

// The fan-out. Its job is to get notifications out to every matching integration and to survive
// each of the ways that can go wrong without taking the others down with it.
public sealed class DispatchNotificationsCommandHandlerTests
{
    private static readonly Guid IncidentId = Guid.NewGuid();

    private readonly IIntegrationRepository _integrations =
        Substitute.For<IIntegrationRepository>();
    private readonly INotificationDeliveryRepository _deliveries =
        Substitute.For<INotificationDeliveryRepository>();
    private readonly INotificationChannelResolver _channels =
        Substitute.For<INotificationChannelResolver>();

    private readonly List<NotificationDelivery> _recorded = [];
    private readonly DispatchNotificationsCommandHandler _handler;

    public DispatchNotificationsCommandHandlerTests()
    {
        _deliveries
            .When(repository =>
                repository.AddAsync(
                    Arg.Any<NotificationDelivery>(),
                    Arg.Any<CancellationToken>()
                )
            )
            .Do(call => _recorded.Add(call.Arg<NotificationDelivery>()));

        _handler = new DispatchNotificationsCommandHandler(
            _integrations,
            _deliveries,
            _channels,
            NullLogger<DispatchNotificationsCommandHandler>.Instance
        );
    }

    // ---- arrangement helpers -------------------------------------------------------------

    private static Integration AnIntegration(
        string name = "ops email",
        NotificationChannelType channel = NotificationChannelType.Email,
        IncidentPriority? minPriority = null,
        string? categoryFilter = null
    ) =>
        Integration.Create(
            name,
            channel,
            new Dictionary<string, string>(),
            minPriority,
            categoryFilter
        );

    private void GivenEnabled(params Integration[] integrations)
    {
        IReadOnlyList<Integration> enabled = integrations;

        _integrations.GetEnabledAsync(Arg.Any<CancellationToken>()).Returns(enabled);
    }

    private INotificationChannel GivenChannel(
        NotificationChannelType channel,
        DeliveryResult? result = null
    )
    {
        var substitute = Substitute.For<INotificationChannel>();
        substitute.Channel.Returns(channel);

        substitute
            .SendAsync(
                Arg.Any<NotificationMessage>(),
                Arg.Any<Integration>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(result ?? DeliveryResult.Success());

        _channels.Resolve(channel).Returns(substitute);

        return substitute;
    }

    private Task Dispatch(string priority = "Critical", string category = "Application") =>
        _handler.Handle(
            new DispatchNotificationsCommand
            {
                EventId = Guid.NewGuid(),
                IncidentId = IncidentId,
                IncidentTitle = "checkout-service: TimeoutException",
                SuggestedPriority = priority,
                SuggestedCategory = category,
                Reasoning = "Repeated timeouts against the payment gateway.",
                Confidence = 0.82,
                AnalyzedAt = DateTime.UtcNow,
            },
            CancellationToken.None
        );

    // ---- the happy path and the filter ----------------------------------------------------

    [Fact]
    public async Task NoEnabledIntegrations_SendsNothing()
    {
        GivenEnabled();

        await Dispatch();

        Assert.Empty(_recorded);
        await _deliveries.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMatchingIntegrationIsNotifiedAndRecordedAsSent()
    {
        GivenEnabled(AnIntegration());
        var channel = GivenChannel(NotificationChannelType.Email);

        await Dispatch();

        await channel
            .Received(1)
            .SendAsync(
                Arg.Any<NotificationMessage>(),
                Arg.Any<Integration>(),
                Arg.Any<CancellationToken>()
            );

        Assert.Equal(DeliveryStatus.Sent, Assert.Single(_recorded).Status);
    }

    [Fact]
    public async Task TheMessageCarriesTheAnalysisThroughToTheChannel()
    {
        NotificationMessage? sent = null;

        GivenEnabled(AnIntegration());
        var channel = GivenChannel(NotificationChannelType.Email);
        channel
            .When(c =>
                c.SendAsync(
                    Arg.Any<NotificationMessage>(),
                    Arg.Any<Integration>(),
                    Arg.Any<CancellationToken>()
                )
            )
            .Do(call => sent = call.Arg<NotificationMessage>());

        await Dispatch(priority: "High", category: "Database");

        Assert.NotNull(sent);
        Assert.Equal(IncidentId, sent.IncidentId);
        Assert.Equal("High", sent.SuggestedPriority);
        Assert.Equal("Database", sent.SuggestedCategory);
        Assert.Equal(0.82, sent.Confidence);
    }

    [Fact]
    public async Task ANonMatchingIntegrationIsSkippedEntirely()
    {
        GivenEnabled(AnIntegration(minPriority: IncidentPriority.Critical));
        var channel = GivenChannel(NotificationChannelType.Email);

        await Dispatch(priority: "Low");

        await channel
            .DidNotReceive()
            .SendAsync(
                Arg.Any<NotificationMessage>(),
                Arg.Any<Integration>(),
                Arg.Any<CancellationToken>()
            );

        Assert.Empty(_recorded);
    }

    [Fact]
    public async Task AnUnrecognisedPriorityIgnoresPriorityFiltersRatherThanDroppingTheIncident()
    {
        // Losing a notification because an analysis produced an unexpected word is worse than
        // sending one a filter might have excluded.
        GivenEnabled(AnIntegration(minPriority: IncidentPriority.Critical));
        GivenChannel(NotificationChannelType.Email);

        await Dispatch(priority: "Catastrophic");

        Assert.Single(_recorded);
    }

    // ---- idempotency ------------------------------------------------------------------------

    [Fact]
    public async Task AnAlreadyDeliveredIncidentIsNotNotifiedAgain()
    {
        // Delivery is at-least-once, so the same event can arrive twice. The delivery row for
        // this (integration, incident) pair is what stops the second copy going out.
        GivenEnabled(AnIntegration());
        var channel = GivenChannel(NotificationChannelType.Email);

        _deliveries
            .ExistsAsync(Arg.Any<Guid>(), IncidentId, Arg.Any<CancellationToken>())
            .Returns(true);

        await Dispatch();

        await channel
            .DidNotReceive()
            .SendAsync(
                Arg.Any<NotificationMessage>(),
                Arg.Any<Integration>(),
                Arg.Any<CancellationToken>()
            );

        Assert.Empty(_recorded);
    }

    [Fact]
    public async Task IdempotencyIsPerIntegration_NotPerIncident()
    {
        // A redelivery after one channel succeeded and another failed must still reach the one
        // that has no row yet.
        var email = AnIntegration("ops email");
        var webhook = AnIntegration("ops webhook", NotificationChannelType.Webhook);

        GivenEnabled(email, webhook);
        var emailChannel = GivenChannel(NotificationChannelType.Email);
        var webhookChannel = GivenChannel(NotificationChannelType.Webhook);

        _deliveries.ExistsAsync(email.Id, IncidentId, Arg.Any<CancellationToken>()).Returns(true);

        await Dispatch();

        await emailChannel
            .DidNotReceive()
            .SendAsync(
                Arg.Any<NotificationMessage>(),
                Arg.Any<Integration>(),
                Arg.Any<CancellationToken>()
            );
        await webhookChannel
            .Received(1)
            .SendAsync(
                Arg.Any<NotificationMessage>(),
                Arg.Any<Integration>(),
                Arg.Any<CancellationToken>()
            );

        Assert.Equal(webhook.Id, Assert.Single(_recorded).IntegrationId);
    }

    // ---- failure isolation --------------------------------------------------------------------

    [Fact]
    public async Task AChannelReportingFailureIsRecordedWithItsReason()
    {
        GivenEnabled(AnIntegration());
        GivenChannel(NotificationChannelType.Email, DeliveryResult.Failure("SMTP 535: bad credentials"));

        await Dispatch();

        var delivery = Assert.Single(_recorded);

        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Equal("SMTP 535: bad credentials", delivery.LastError);
    }

    [Fact]
    public async Task AChannelThrowingDoesNotStopTheOthers()
    {
        // Isolated per integration on purpose: one broken channel must not stop the rest, and
        // must not bubble up and get the message dead-lettered after others have delivered.
        var broken = AnIntegration("broken jira", NotificationChannelType.Jira);
        var working = AnIntegration("ops email");

        GivenEnabled(broken, working);

        var jira = GivenChannel(NotificationChannelType.Jira);
        jira.SendAsync(
                Arg.Any<NotificationMessage>(),
                Arg.Any<Integration>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var email = GivenChannel(NotificationChannelType.Email);

        await Dispatch();

        await email
            .Received(1)
            .SendAsync(
                Arg.Any<NotificationMessage>(),
                Arg.Any<Integration>(),
                Arg.Any<CancellationToken>()
            );

        Assert.Equal(2, _recorded.Count);
        Assert.Equal(DeliveryStatus.Failed, _recorded[0].Status);
        Assert.Equal("connection refused", _recorded[0].LastError);
        Assert.Equal(DeliveryStatus.Sent, _recorded[1].Status);
    }

    [Fact]
    public async Task AnUnresolvableChannelIsRecordedAsFailedRatherThanThrowing()
    {
        GivenEnabled(AnIntegration());

        _channels
            .Resolve(NotificationChannelType.Email)
            .Returns(_ => throw new InvalidOperationException("no channel registered"));

        await Dispatch();

        Assert.Equal(DeliveryStatus.Failed, Assert.Single(_recorded).Status);
    }

    // ---- persistence ----------------------------------------------------------------------------

    [Fact]
    public async Task AFailureToPersistTheAuditRowsIsSwallowed()
    {
        // The notifications have already gone out. Rethrowing would dead-letter the message and
        // a later redelivery would send them all again, so losing the audit rows is the lesser
        // failure — and the only honest one.
        GivenEnabled(AnIntegration());
        GivenChannel(NotificationChannelType.Email);

        _deliveries
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("connection reset"));

        var exception = await Record.ExceptionAsync(() => Dispatch());

        Assert.Null(exception);
    }

    [Fact]
    public async Task TheBatchIsSavedOnce()
    {
        GivenEnabled(AnIntegration("ops email"), AnIntegration("ops webhook", NotificationChannelType.Webhook));
        GivenChannel(NotificationChannelType.Email);
        GivenChannel(NotificationChannelType.Webhook);

        await Dispatch();

        await _deliveries.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
