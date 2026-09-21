using System.Text.Json.Serialization;

namespace NotificationService.Domain.Enums;

// Deliberately a local copy of the IncidentService enum: two service domains must not reference
// each other, and IncidentAnalyzedEvent carries the priority as a string. Declaration order is
// the severity order — Critical is the most severe, so a lower value means more severe.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IncidentPriority
{
    Critical,
    High,
    Medium,
    Low
}
