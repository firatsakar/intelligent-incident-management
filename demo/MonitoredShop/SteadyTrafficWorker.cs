namespace MonitoredShop;

// Background noise. Without it every error in the store is an error, and the rate baseline has
// nothing to be a baseline of.
public sealed class SteadyTrafficWorker : BackgroundService
{
    private static readonly TimeSpan OrderInterval = TimeSpan.FromSeconds(2);
    private static readonly string[] Regions = ["eu-west", "eu-central", "us-east"];

    private readonly ILogger<SteadyTrafficWorker> _logger;

    public SteadyTrafficWorker(ILogger<SteadyTrafficWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tick = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            var orderId = Random.Shared.Next(100_000, 999_999);
            var region = Regions[Random.Shared.Next(Regions.Length)];

            _logger.LogInformation(
                "Order {OrderId} placed from {Region} for {Amount:F2} EUR",
                orderId,
                region,
                Random.Shared.Next(1500, 45000) / 100d
            );

            // An occasional unremarkable warning — the kind of thing that should never on its own
            // become an incident.
            if (++tick % 7 == 0)
            {
                _logger.LogWarning(
                    "Product image CDN responded slowly for order {OrderId} ({DurationMs}ms)",
                    orderId,
                    Random.Shared.Next(800, 2500)
                );
            }

            await Task.Delay(OrderInterval, stoppingToken);
        }
    }
}
