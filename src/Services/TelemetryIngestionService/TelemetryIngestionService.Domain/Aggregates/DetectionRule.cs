using BuildingBlocks.SharedKernel;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Domain.Aggregates;

// Thresholds live in the database, not in code, so they can be tuned under pressure without a
// deploy — which is exactly when tuning them matters.
public sealed class DetectionRule : AggregateRoot
{
    private DetectionRule() { }

    public string Name { get; private set; } = default!;

    // Null matches every service — the default rule.
    public string? Service { get; private set; }

    // Records at or above this severity are evaluated.
    public LogSeverity MinSeverity { get; private set; }

    public int WindowSeconds { get; private set; }
    public int Threshold { get; private set; }
    public int DedupWindowHours { get; private set; }
    public double PromoteThreshold { get; private set; }
    public bool IsEnabled { get; private set; }

    public static DetectionRule Create(
        string name,
        string? service,
        LogSeverity minSeverity,
        int windowSeconds,
        int threshold,
        int dedupWindowHours,
        double promoteThreshold,
        bool isEnabled = true
    )
    {
        return new DetectionRule
        {
            Id = Guid.NewGuid(),
            Name = name,
            Service = service,
            MinSeverity = minSeverity,
            WindowSeconds = windowSeconds,
            Threshold = threshold,
            DedupWindowHours = dedupWindowHours,
            PromoteThreshold = promoteThreshold,
            IsEnabled = isEnabled,
        };
    }

    public void Update(
        string name,
        LogSeverity minSeverity,
        int windowSeconds,
        int threshold,
        int dedupWindowHours,
        double promoteThreshold
    )
    {
        Name = name;
        MinSeverity = minSeverity;
        WindowSeconds = windowSeconds;
        Threshold = threshold;
        DedupWindowHours = dedupWindowHours;
        PromoteThreshold = promoteThreshold;
        SetUpdatedAt();
    }

    public void Enable()
    {
        IsEnabled = true;
        SetUpdatedAt();
    }

    public void Disable()
    {
        IsEnabled = false;
        SetUpdatedAt();
    }

    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);

    public TimeSpan DedupWindow => TimeSpan.FromHours(DedupWindowHours);

    // A service-specific rule wins over the catch-all.
    public bool AppliesTo(string service)
    {
        return Service is null || string.Equals(Service, service, StringComparison.OrdinalIgnoreCase);
    }
}
