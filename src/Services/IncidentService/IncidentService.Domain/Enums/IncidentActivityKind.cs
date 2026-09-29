using System.Text.Json.Serialization;

namespace IncidentService.Domain.Enums;

/// <summary>What happened to an incident, as its activity trail records it (Adım 14).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IncidentActivityKind
{
    Opened,
    StatusChanged,
    TeamAssigned,
    AnalysisApplied,
    AnalysisFailed,
    Commented,
}
