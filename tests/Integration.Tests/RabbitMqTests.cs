using System.Net;
using System.Net.Sockets;
using BuildingBlocks.EventBus;
using BuildingBlocks.SharedKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.RabbitMq;

namespace Integration.Tests;

// The bus against a real broker (Adım 23): the two behaviours every service relies on and no unit
// test can reach, because RabbitMqConnection is the real client.
public sealed class RabbitMqTests
{
    private const string User = "iim";
    private const string Password = "integration-tests";

    public sealed record ProbeEvent : IntegrationEvent
    {
        public required string Note { get; init; }
    }

    private sealed class ProbeHandler(TaskCompletionSource<ProbeEvent> received) : IIntegrationEventHandler<ProbeEvent>
    {
        public Task HandleAsync(ProbeEvent integrationEvent, CancellationToken cancellationToken = default)
        {
            received.TrySetResult(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static RabbitMqContainer Broker(int port) =>
        new RabbitMqBuilder("rabbitmq:3")
            .WithUsername(User)
            .WithPassword(Password)
            .WithPortBinding(port, RabbitMqBuilder.RabbitMqPort)
            .Build();

    /// <summary>One service's bus: its own queue, its own handler, the broker on <paramref name="port"/>.</summary>
    private static (ServiceProvider Provider, TaskCompletionSource<ProbeEvent> Received) Service(string name, int port)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["EventBus:Host"] = "127.0.0.1",
                    ["EventBus:Port"] = port.ToString(),
                    ["EventBus:UserName"] = User,
                    ["EventBus:Password"] = Password,
                    ["EventBus:ExchangeName"] = "iim_integration_tests",
                    ["EventBus:SubscriptionClientName"] = name,
                    // One try per attempt: the subscription's own retries are what is under test.
                    ["EventBus:RetryCount"] = "0",
                }
            )
            .Build();

        var received = new TaskCompletionSource<ProbeEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IOrganizationContext, OrganizationContext>();
        services.AddSingleton(new ProbeHandler(received));
        services.AddRabbitMqEventBus(configuration);

        return (services.BuildServiceProvider(), received);
    }

    /// <summary>Publishes until <paramref name="until"/> completes: a binding in progress drops what it has not bound yet.</summary>
    private static async Task PublishUntilAsync(IEventBus publisher, Task until, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (!until.IsCompleted && DateTime.UtcNow < deadline)
        {
            try
            {
                await publisher.PublishAsync(new ProbeEvent { OrganizationId = Guid.NewGuid(), Note = "probe" });
            }
            catch
            {
                // The broker may still be starting.
            }

            await Task.WhenAny(until, Task.Delay(500));
        }
    }

    [Fact]
    public async Task ASubscriptionMadeBeforeTheBrokerIsUpBindsOnceItIs()
    {
        var port = FreePort();
        var (subscriber, received) = Service("late-broker-subscriber", port);
        var (publisher, _) = Service("late-broker-publisher", port);

        await using (subscriber)
        await using (publisher)
        {
            // Nothing is listening yet. Until Adım 28 this attempt failed once, logged it, and the
            // service never received anything.
            subscriber.GetRequiredService<IEventBus>().Subscribe<ProbeEvent, ProbeHandler>();

            await Task.Delay(TimeSpan.FromSeconds(3));

            await using var broker = Broker(port);
            await broker.StartAsync();

            await PublishUntilAsync(publisher.GetRequiredService<IEventBus>(), received.Task, TimeSpan.FromSeconds(90));

            Assert.True(received.Task.IsCompletedSuccessfully, "The late subscription never received an event.");
        }
    }

    [Fact]
    public async Task TwoServicesSubscribedToOneEventEachGetACopy()
    {
        var port = FreePort();

        await using var broker = Broker(port);
        await broker.StartAsync();

        var (incident, incidentReceived) = Service("incident-service-copy", port);
        var (notification, notificationReceived) = Service("notification-service-copy", port);

        await using (incident)
        await using (notification)
        {
            incident.GetRequiredService<IEventBus>().Subscribe<ProbeEvent, ProbeHandler>();
            notification.GetRequiredService<IEventBus>().Subscribe<ProbeEvent, ProbeHandler>();

            // Queue per service, not per event type: a shared queue would hand each event to one of
            // them only, and one of these would wait forever.
            await PublishUntilAsync(
                incident.GetRequiredService<IEventBus>(),
                Task.WhenAll(incidentReceived.Task, notificationReceived.Task),
                TimeSpan.FromSeconds(60)
            );

            Assert.True(incidentReceived.Task.IsCompletedSuccessfully);
            Assert.True(notificationReceived.Task.IsCompletedSuccessfully);
        }
    }
}
