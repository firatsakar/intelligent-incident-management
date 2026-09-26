using IncidentService.Domain.Enums;

namespace IncidentService.API.Contracts;

/// <summary>
/// The one body the incident API accepts (Adım 27). Nullable throughout so a missing field is
/// answered by the validator, in the sender's field names, rather than by the JSON reader.
/// </summary>
public sealed record IncidentIntakeRequest
{
    public string? Title { get; init; }
    public string? Description { get; init; }

    /// <summary>Critical, High, Medium or Low. Medium when left out; the analysis suggests one anyway.</summary>
    public IncidentPriority? Priority { get; init; }

    /// <summary>The sender's name for the problem — an alert fingerprint, a check id.</summary>
    public string? ExternalId { get; init; }

    /// <summary>When the problem started, if not now.</summary>
    public DateTime? DetectedAt { get; init; }
}
