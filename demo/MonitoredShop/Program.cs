using MonitoredShop;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logs go to the demo log store only — never to the platform's own Seq. Keeping the two streams
// apart is the whole point: IIM monitors this application, it does not monitor itself.
builder.Host.UseSerilog(
    (context, configuration) =>
        configuration
            .MinimumLevel.Information()
            .Enrich.WithProperty("Service", ShopOptions.ServiceName)
            .WriteTo.Console()
            .WriteTo.Seq(
                context.Configuration["Seq:Url"] ?? ShopOptions.DefaultSeqUrl,
                // Small batches keep the demo responsive: a triggered storm should show up in
                // seconds, not on the sink's default flush interval.
                period: TimeSpan.FromSeconds(1)
            )
);

builder.Services.AddHostedService<SteadyTrafficWorker>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = ShopOptions.ServiceName, status = "running" }));

// A target for exercising webhook notification integrations. It lives here rather than inside
// NotificationService because it is demo scaffolding, and a product service should not ship
// endpoints that exist only to be pointed at during testing. Accepts any trailing path, so it can
// also stand in for an API that appends its own — which is how the Jira channel's request shape
// was verified before a real token existed.
app.MapPost(
    "/echo/{**path}",
    async (string? path, HttpRequest request, ILogger<Program> logger) =>
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync();

        // Header names only — values can carry credentials.
        logger.LogInformation(
            "Echo received POST /{Path} with headers [{HeaderNames}] and payload: {Payload}",
            path ?? string.Empty,
            string.Join(", ", request.Headers.Select(header => header.Key)),
            body
        );

        return Results.Ok(new { receivedAt = DateTime.UtcNow, path, body });
    }
);

// Emits a burst of failures that all share one signature: same exception type, same message
// template, only the ids vary. That is exactly the case fingerprinting has to collapse.
app.MapPost(
    "/chaos/error-storm",
    (int? count, ILogger<Program> logger) =>
    {
        var total = Math.Clamp(count ?? 10, 1, 500);

        foreach (var _ in Enumerable.Range(0, total))
        {
            var orderId = Random.Shared.Next(100_000, 999_999);
            var customerId = $"c-{Random.Shared.Next(1000, 9999)}";

            try
            {
                throw new InvalidOperationException(
                    $"Npgsql connection pool exhausted while placing order {orderId}"
                );
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Checkout failed for order {OrderId} of customer {CustomerId}",
                    orderId,
                    customerId
                );
            }
        }

        return Results.Ok(new { emitted = total, kind = "error-storm" });
    }
);

// A single fatal, for the path that promotes without waiting for a threshold.
app.MapPost(
    "/chaos/fatal",
    (ILogger<Program> logger) =>
    {
        try
        {
            throw new OutOfMemoryException("Checkout worker exhausted its memory limit");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Checkout worker terminated unexpectedly");
        }

        return Results.Ok(new { emitted = 1, kind = "fatal" });
    }
);

// A second, unrelated signature — proves signatures are tracked independently rather than
// lumped together by service.
app.MapPost(
    "/chaos/timeout-storm",
    (int? count, ILogger<Program> logger) =>
    {
        var total = Math.Clamp(count ?? 10, 1, 500);

        foreach (var _ in Enumerable.Range(0, total))
        {
            var cartId = Guid.NewGuid();

            try
            {
                throw new TimeoutException(
                    $"Payment gateway did not respond within 30s for cart {cartId}"
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Payment authorization timed out for cart {CartId}", cartId);
            }
        }

        return Results.Ok(new { emitted = total, kind = "timeout-storm" });
    }
);

app.Run();
