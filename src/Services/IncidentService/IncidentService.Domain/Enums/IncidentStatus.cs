using System.Text.Json.Serialization;

namespace IncidentService.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IncidentStatus
{
    Open,
    InProgress,
    Resolved,
    Closed
}