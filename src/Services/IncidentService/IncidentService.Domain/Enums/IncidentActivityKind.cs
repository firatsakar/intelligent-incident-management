using System.Text.Json.Serialization;

namespace IncidentService.Domain.Enums;

/// <summary>What happened to an incident, as its activity trail records it.</summary>
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
