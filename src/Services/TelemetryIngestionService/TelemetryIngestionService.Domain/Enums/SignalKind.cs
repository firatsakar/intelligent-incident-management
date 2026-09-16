using System.Text.Json.Serialization;

namespace TelemetryIngestionService.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SignalKind
{
    // A signature breached its occurrence threshold within the detection window.
    LogBurst,

    // A signature's error rate deviated from its own rolling baseline.
    RateAnomaly
}
