using NotificationService.Application.Abstractions;
using NotificationService.Application.Commands.CreateIntegration;
using NotificationService.Application.Commands.DeleteIntegration;
using NotificationService.Application.Commands.SetIntegrationEnabled;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Aggregates;
using NotificationService.Domain.Enums;
using NSubstitute;

namespace NotificationService.Tests;

// Configuration used to change with nobody told. A second operator — or the same one in a second
// tab — could be looking at a channel that had been renamed, paused or deleted underneath them,
// and the only way to find out was to reload.
public sealed class IntegrationRealtimeTests
{
    private readonly IIntegrationRepository _integrations =
        Substitute.For<IIntegrationRepository>();
    private readonly IRealtimeNotifier _realtime = Substitute.For<IRealtimeNotifier>();

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
                ["Password"] = "the-real-secret",
            },
            minPriority: null,
            categoryFilter: null
        );

    [Fact]
    public async Task CreatingAnIntegrationAnnouncesIt()
    {
        var handler = new CreateIntegrationCommandHandler(_integrations, _realtime);

        await handler.Handle(
            new CreateIntegrationCommand
            {
                Name = "Ops mailbox",
                Channel = NotificationChannelType.Email,
                Config = new Dictionary<string, string>
                {
                    ["Host"] = "localhost",
                    ["Port"] = "1025",
                    ["From"] = "incidents@example.com",
                    ["To"] = "oncall@example.com",
                },
                IsEnabled = true,
            },
            CancellationToken.None
        );

        await _realtime
            .Received(1)
            .IntegrationChangedAsync(
                Arg.Is<IntegrationDto>(dto => dto.Name == "Ops mailbox"),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task TheAnnouncementComesAfterTheSave()
    {
        // What is broadcast has to be what is stored. Announcing first would push a row that a
        // failing save then leaves nonexistent.
        var handler = new CreateIntegrationCommandHandler(_integrations, _realtime);

        await handler.Handle(
            new CreateIntegrationCommand
            {
                Name = "Ops mailbox",
                Channel = NotificationChannelType.Webhook,
                Config = new Dictionary<string, string> { ["Url"] = "https://example.com" },
                IsEnabled = true,
            },
            CancellationToken.None
        );

        Received.InOrder(() =>
        {
            _integrations.SaveChangesAsync(Arg.Any<CancellationToken>());
            _realtime.IntegrationChangedAsync(
                Arg.Any<IntegrationDto>(),
                Arg.Any<CancellationToken>()
            );
        });
    }

    [Fact]
    public async Task TheBroadcastCarriesTheMaskedConfig_NotTheRealOne()
    {
        // The push travels the same path a GET does, so it must not become the one place a
        // credential leaves the service in the clear. Every connected client receives this.
        var integration = EmailIntegration();

        _integrations
            .GetByIdAsync(integration.Id, Arg.Any<CancellationToken>())
            .Returns(integration);

        var handler = new SetIntegrationEnabledCommandHandler(_integrations, _realtime);

        await handler.Handle(
            new SetIntegrationEnabledCommand(integration.Id, false),
            CancellationToken.None
        );

        await _realtime
            .Received(1)
            .IntegrationChangedAsync(
                Arg.Is<IntegrationDto>(dto =>
                    dto.Config["Password"] != "the-real-secret" && !dto.IsEnabled
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task DeletingAnIntegrationAnnouncesTheIdAlone()
    {
        // There is no DTO left to carry, which is the whole reason this is its own message
        // rather than a changed-with-a-flag.
        var integration = EmailIntegration();

        _integrations
            .GetByIdAsync(integration.Id, Arg.Any<CancellationToken>())
            .Returns(integration);

        var handler = new DeleteIntegrationCommandHandler(_integrations, _realtime);

        await handler.Handle(
            new DeleteIntegrationCommand(integration.Id),
            CancellationToken.None
        );

        await _realtime
            .Received(1)
            .IntegrationDeletedAsync(integration.Id, Arg.Any<CancellationToken>());
        await _realtime
            .DidNotReceive()
            .IntegrationChangedAsync(
                Arg.Any<IntegrationDto>(),
                Arg.Any<CancellationToken>()
            );
    }
}
