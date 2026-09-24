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

    /// <summary>
    /// Whose source this is, and therefore whose every log record, signature, signal and incident
    /// downstream of it is.
    /// </summary>
    /// <remarks>
    /// This is the root of the scope for the whole detection pipeline. Nothing in that pipeline
    /// runs on a request — a background loop polls, detects and promotes — so there is no claim to
    /// read anywhere along it. Every organisation the pipeline ever establishes is read from this
    /// column and then carried on the messages.
    /// </remarks>
    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = default!;
    public TelemetrySourceKind Kind { get; private set; }
    public bool IsEnabled { get; private set; }

    // Connector-specific settings — URL, API key, query filter. Kept as a key/value bag because
    // every connector needs a different shape.
    public IReadOnlyDictionary<string, string> Config => _config;

    public int PollIntervalSeconds { get; private set; }

    public static TelemetrySource Create(
        Guid organizationId,
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
            OrganizationId = organizationId,
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
