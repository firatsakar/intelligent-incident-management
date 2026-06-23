using System.Text.Json.Serialization;

namespace IncidentService.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IncidentPriority
{
    Critical,
    High,
    Medium,
    Low
}
