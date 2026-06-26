using System.Text.Json.Serialization;

namespace AgentOrchestrator.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AnalysisStatus
{
    Pending,
    Completed,
    Failed,
}
