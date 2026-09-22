using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IncidentService.Infrastructure.Persistence;

/// <summary>
/// Backdated incidents for a demo, written straight through the DbContext.
///
/// The API cannot do this. <c>CreatedAt</c> has a protected setter on the shared Entity base, so
/// every incident created through <c>POST /api/incidents</c> is stamped "now" — and a chart of
/// arrivals over thirty days built from that is one bar. Opening the setter for a seeder would
/// mean opening it for production code too, which is the wrong trade for a demo, so the value is
/// set through EF's change tracker instead and the domain stays exactly as it was.
///
/// It also bypasses the command path on purpose: seeding N rows through it would fire N list
/// invalidations in every browser that happens to be open.
///
/// Infrastructure rather than Application, because reaching into the change tracker is a
/// persistence concern and nothing in the Application layer should learn that it can.
/// </summary>
public sealed class DemoIncidentSeeder
{
    /// <summary>
    /// Written into the description of every generated row. It is how a second call recognises
    /// the first one's work, and how anyone looking at the database can tell demo data from a
    /// customer's.
    /// </summary>
    public const string Marker = "[demo-seed]";

    private const int Days = 30;

    private readonly IncidentDbContext _context;

    public DemoIncidentSeeder(IncidentDbContext context)
    {
        _context = context;
    }

    public sealed record SeedResult(int Created, int AlreadyPresent);

    public async Task<SeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _context
            .Incidents.AsNoTracking()
            .CountAsync(x => x.Description.Contains(Marker), cancellationToken);

        // Idempotent: calling twice leaves what calling once left. A demo endpoint that
        // multiplies its own data every time somebody presses it is worse than no endpoint.
        if (existing > 0)
            return new SeedResult(0, existing);

        // A fixed seed, so two runs of the demo produce the same shape. A chart that looks
        // different every time it is shown cannot be talked about.
        var random = new Random(20260922);
        var now = DateTime.UtcNow;
        var created = 0;

        for (var dayOffset = Days - 1; dayOffset >= 0; dayOffset--)
        {
            // A quiet baseline with occasional bad days, rather than a uniform sprinkle. The
            // whole reason to draw this chart is that incident arrivals are bursty, and a flat
            // random series would make the screen argue the opposite.
            var isBadDay = dayOffset % 7 == 3 || dayOffset % 11 == 0;
            var count = isBadDay ? random.Next(5, 10) : random.Next(0, 3);

            for (var i = 0; i < count; i++)
            {
                var createdAt = now.AddDays(-dayOffset)
                    .Date.AddHours(random.Next(0, 24))
                    .AddMinutes(random.Next(0, 60));

                // Today's bucket is only part of a day. Picking an hour out of twenty-four would
                // date some rows in the future, and every window ends at "now" — so the totals
                // on two screens reading the same data would disagree by however many rows
                // landed past the end.
                if (createdAt > now)
                    createdAt = now.AddMinutes(-random.Next(1, 90));

                Add(random, createdAt, isBadDay);
                created++;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new SeedResult(created, 0);
    }

    private void Add(Random random, DateTime createdAt, bool isBadDay)
    {
        var scenario = Scenarios[random.Next(Scenarios.Length)];

        // Telemetry incidents carry a detection time; hand-filed ones do not. Keeping both in the
        // data is what makes the detection-latency figure mean anything — a number with nothing
        // to contrast against is just a number.
        var source = scenario.Source;

        DateTime? detectedAt = source == IncidentSource.Manual
            ? null
            : createdAt.AddSeconds(-random.Next(30, 400));

        var priority = isBadDay ? scenario.PriorityWhenBad : scenario.PriorityWhenQuiet;

        var incident = Incident.Create(
            scenario.Title,
            $"{scenario.Description} {Marker}",
            priority,
            source,
            assignedTeam: scenario.Team,
            detectedAt: detectedAt
        );

        // Roughly three in four are analysed, which is what the real chain produces: analysis is
        // asynchronous, so the most recent arrivals are legitimately still waiting.
        var roll = random.Next(0, 100);

        if (roll < 70)
        {
            incident.ApplyAiAnalysis(
                priority,
                scenario.Category,
                scenario.Reasoning,
                Math.Round(0.55 + random.NextDouble() * 0.44, 2)
            );
        }
        else if (roll < 78)
        {
            // A small slice failed, because that state exists now and a demo that never shows it
            // leaves the screen untested against its own unhappy path.
            incident.RecordAiAnalysisFailure(
                "The model is overloaded. Please retry your request after a short delay."
            );
        }

        // Statuses spread across the lifecycle, weighted towards resolved for older days.
        var status = StatusFor(random, createdAt);

        if (status != IncidentStatus.Open)
            incident.UpdateStatus(status);

        _context.Incidents.Add(incident);

        // The only reason this class exists. CreatedAt and UpdatedAt are protected on the entity,
        // so they are written through the change tracker after Add — which is exactly as far as
        // the exception goes: no setter is opened, and nothing outside Infrastructure can do it.
        var entry = _context.Entry(incident);
        entry.Property(x => x.CreatedAt).CurrentValue = createdAt;

        if (incident.UpdatedAt.HasValue)
            entry.Property(x => x.UpdatedAt).CurrentValue = createdAt.AddMinutes(random.Next(2, 90));
    }

    private static IncidentStatus StatusFor(Random random, DateTime createdAt)
    {
        var ageInDays = (DateTime.UtcNow - createdAt).TotalDays;
        var roll = random.Next(0, 100);

        // Old incidents are mostly finished and recent ones mostly are not, which is what makes
        // the open-incident panel show a believable handful rather than a month's worth.
        if (ageInDays > 14)
            return roll < 80 ? IncidentStatus.Closed : IncidentStatus.Resolved;

        if (ageInDays > 5)
            return roll < 55 ? IncidentStatus.Resolved : IncidentStatus.InProgress;

        return roll < 60 ? IncidentStatus.Open : IncidentStatus.InProgress;
    }

    private sealed record Scenario(
        string Title,
        string Description,
        string Category,
        string Reasoning,
        string? Team,
        IncidentSource Source,
        IncidentPriority PriorityWhenBad,
        IncidentPriority PriorityWhenQuiet
    );

    // Deliberately varied across service, category, source and severity: the breakdowns on the
    // dashboard are only worth drawing if the underlying data actually differs along those axes.
    private static readonly Scenario[] Scenarios =
    [
        new(
            "checkout-service: TimeoutException",
            "Payment authorisation timed out for cart.",
            "Application",
            "A recurring timeout against the payment provider, clustered in short bursts.",
            "payments",
            IncidentSource.Telemetry,
            IncidentPriority.High,
            IncidentPriority.Medium
        ),
        new(
            "checkout-service: OutOfMemoryException",
            "Checkout worker terminated unexpectedly.",
            "Application",
            "The worker exhausted its memory limit; a fatal crash bypasses the burst threshold.",
            "platform",
            IncidentSource.Telemetry,
            IncidentPriority.Critical,
            IncidentPriority.High
        ),
        new(
            "search-service: connection pool exhausted",
            "Npgsql connection pool exhausted under load.",
            "Database",
            "Pool saturation under sustained read load; queries queue rather than fail outright.",
            "search",
            IncidentSource.Telemetry,
            IncidentPriority.High,
            IncidentPriority.Medium
        ),
        new(
            "Elasticsearch cluster rejecting writes",
            "Index writes rejected after the disk watermark was crossed.",
            "Infrastructure",
            "The flood-stage watermark makes indices read-only until space is reclaimed.",
            "platform",
            IncidentSource.Alert,
            IncidentPriority.Critical,
            IncidentPriority.High
        ),
        new(
            "Payment API returns 504 on checkout",
            "Upstream gateway timed out for EU customers.",
            "Infrastructure",
            "An upstream dependency, confined to one region by the traffic pattern.",
            "payments",
            IncidentSource.Manual,
            IncidentPriority.High,
            IncidentPriority.Low
        ),
        new(
            "notification-service: SMTP handshake failure",
            "The mail relay refused the connection during dispatch.",
            "Integration",
            "Delivery failures cluster around the relay's maintenance window.",
            "platform",
            IncidentSource.Manual,
            IncidentPriority.Medium,
            IncidentPriority.Low
        ),
        new(
            "inventory-service: stale cache served",
            "Stock levels served from an expired cache entry.",
            "Application",
            "Cache invalidation lags behind the write path under concurrent updates.",
            "inventory",
            IncidentSource.Telemetry,
            IncidentPriority.Medium,
            IncidentPriority.Low
        ),
        new(
            "auth-service: token validation latency",
            "Token introspection exceeded its latency budget.",
            "Security",
            "Introspection round trips grow with session count; no failures, only latency.",
            "security",
            IncidentSource.Alert,
            IncidentPriority.Medium,
            IncidentPriority.Low
        ),
    ];
}
