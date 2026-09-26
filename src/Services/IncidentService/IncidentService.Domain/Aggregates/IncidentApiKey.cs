using BuildingBlocks.SharedKernel;

namespace IncidentService.Domain.Aggregates;

/// <summary>
/// A key an external system opens incidents with — a script, a CI pipeline, an organisation's own
/// alerting — without anybody signing in (Adım 27).
/// </summary>
/// <remarks>
/// <para>
/// It belongs to the organisation, not to the Admin who made it: an integration should not stop
/// the day that person leaves. It can do one thing, open an incident, and nothing it sends back
/// reveals anything but the id of what it opened — a leaked key is a nuisance, not a breach.
/// </para>
/// <para>
/// Only the hash is kept (<see cref="AccessKey"/>). Rotating is making a second key, moving the
/// sender to it and deleting the first, which never leaves a sender without a working key.
/// </para>
/// </remarks>
public sealed class IncidentApiKey : AggregateRoot
{
    public const string Prefix = "iim_inc_";

    public const int NameMaxLength = 64;

    // A key that is used continuously would otherwise write a row on every incident it sends.
    private static readonly TimeSpan UsageResolution = TimeSpan.FromMinutes(1);

    private IncidentApiKey() { }

    public Guid OrganizationId { get; private set; }

    /// <summary>What the Admin called it — shown on every incident it opens.</summary>
    public string Name { get; private set; } = default!;

    public string KeyHash { get; private set; } = default!;

    public string KeyPrefix { get; private set; } = default!;

    /// <summary>The Admin's display name when it was made. A name, not a reference: it outlives them.</summary>
    public string CreatedBy { get; private set; } = default!;

    public DateTime? LastUsedAt { get; private set; }

    /// <summary>A new key, and the only copy of its value that will ever exist.</summary>
    public static (IncidentApiKey Key, string Secret) Issue(Guid organizationId, string name, string createdBy)
    {
        var issued = AccessKey.Generate(Prefix);

        var key = new IncidentApiKey
        {
            OrganizationId = organizationId,
            Name = name.Trim(),
            KeyHash = issued.Hash,
            KeyPrefix = issued.DisplayPrefix,
            CreatedBy = createdBy,
        };

        return (key, issued.Key);
    }

    /// <summary>Records a use, to the minute. True when that changed anything worth saving.</summary>
    public bool MarkUsed(DateTime now)
    {
        if (LastUsedAt is { } last && now - last < UsageResolution)
            return false;

        LastUsedAt = now;

        return true;
    }
}
