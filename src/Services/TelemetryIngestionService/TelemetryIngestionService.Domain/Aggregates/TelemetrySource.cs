using BuildingBlocks.SharedKernel;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Domain.Aggregates;

// A customer-configured external system that logs are pulled from. Same shape as
// NotificationService's Integration: the platform owns the mechanism, the customer owns the values.
public sealed class TelemetrySource : AggregateRoot
{
    public const int DefaultPollIntervalSeconds = 30;

    private Dictionary<string, string> _config = new();

    private TelemetrySource() { }

    public string Name { get; private set; } = default!;
    public TelemetrySourceKind Kind { get; private set; }
    public bool IsEnabled { get; private set; }

    // Connector-specific settings — URL, API key, query filter. Kept as a key/value bag because
    // every connector needs a different shape.
    public IReadOnlyDictionary<string, string> Config => _config;

    public int PollIntervalSeconds { get; private set; }

    public static TelemetrySource Create(
        string name,
        TelemetrySourceKind kind,
        IReadOnlyDictionary<string, string> config,
        int? pollIntervalSeconds = null,
        bool isEnabled = true
    )
    {
        return new TelemetrySource
        {
            Id = Guid.NewGuid(),
            Name = name,
            Kind = kind,
            _config = new Dictionary<string, string>(config),
            PollIntervalSeconds = pollIntervalSeconds ?? DefaultPollIntervalSeconds,
            IsEnabled = isEnabled,
        };
    }

    public void Update(string name, int? pollIntervalSeconds)
    {
        Name = name;
        PollIntervalSeconds = pollIntervalSeconds ?? DefaultPollIntervalSeconds;
        SetUpdatedAt();
    }

    public void UpdateConfig(IReadOnlyDictionary<string, string> config)
    {
        _config = new Dictionary<string, string>(config);
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
}
