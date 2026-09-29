using System.Text.Json.Serialization;

namespace IncidentService.Domain.Enums;

/// <summary>
/// Who did something to an incident: a person signed in to the console, an external system with
/// an incident API key, the telemetry detector, or the AI analysis.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ActivityActorKind
{
    User,
    ApiKey,
    Detector,
    Ai,
}
