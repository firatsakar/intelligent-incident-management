using System.Text.Json.Serialization;

namespace IncidentService.Domain.Enums;

/// <summary>
/// What the people who worked an incident concluded when they closed it. Telemetry learns from
/// this: a signature whose incidents keep turning out to be false alarms is scored lower the next
/// time it fires.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IncidentVerdict
{
    Real,
    FalsePositive,
}
