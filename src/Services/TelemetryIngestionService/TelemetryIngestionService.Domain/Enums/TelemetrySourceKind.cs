using System.Text.Json.Serialization;

namespace TelemetryIngestionService.Domain.Enums;

// Which external system a source is pulled from. Seq is the first connector because it is already
// in the stack; Elasticsearch and a generic HTTP source are the next candidates.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TelemetrySourceKind
{
    Seq
}
