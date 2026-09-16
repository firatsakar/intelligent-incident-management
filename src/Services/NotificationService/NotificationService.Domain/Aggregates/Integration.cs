using BuildingBlocks.SharedKernel;
using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Aggregates;

public sealed class Integration : AggregateRoot
{
    private Dictionary<string, string> _config = new();

    private Integration() { }

    public string Name { get; private set; } = default!;
    public NotificationChannelType Channel { get; private set; }
    public bool IsEnabled { get; private set; }

    // Channel-specific settings — SMTP host and recipients, webhook URL, Jira project key and so
    // on. A key/value bag rather than columns: every channel needs a different shape, and the
    // values belong to the customer, not to us.
    public IReadOnlyDictionary<string, string> Config => _config;

    // Both filters are optional; null means "no filter". MinPriority is inclusive and compares by
    // severity, so MinPriority = High also matches Critical.
    public IncidentPriority? MinPriority { get; private set; }
    public string? CategoryFilter { get; private set; }

    public static Integration Create(
        string name,
        NotificationChannelType channel,
        IReadOnlyDictionary<string, string> config,
        IncidentPriority? minPriority = null,
        string? categoryFilter = null,
        bool isEnabled = true
    )
    {
        return new Integration
        {
            Id = Guid.NewGuid(),
            Name = name,
            Channel = channel,
            _config = new Dictionary<string, string>(config),
            MinPriority = minPriority,
            CategoryFilter = categoryFilter,
            IsEnabled = isEnabled,
        };
    }

    public void Update(string name, IncidentPriority? minPriority, string? categoryFilter)
    {
        Name = name;
        MinPriority = minPriority;
        CategoryFilter = categoryFilter;
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

    // Severity runs Critical(0) to Low(3), so "at least as severe as MinPriority" is a <= test.
    // A null priority means the analysis produced something unrecognisable; the priority filter is
    // then skipped rather than applied, because silently dropping an incident notification is
    // worse than sending one the filter might have excluded.
    public bool Matches(IncidentPriority? priority, string category)
    {
        if (MinPriority.HasValue && priority.HasValue && priority.Value > MinPriority.Value)
            return false;

        if (
            !string.IsNullOrWhiteSpace(CategoryFilter)
            && !string.Equals(CategoryFilter, category, StringComparison.OrdinalIgnoreCase)
        )
            return false;

        return true;
    }
}
