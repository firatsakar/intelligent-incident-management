using System.Text.Json.Serialization;

namespace TelemetryIngestionService.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SignalStatus
{
    // Scored below the weak band: recorded only, feeds the baseline and the precedent weight.
    Recorded,

    // Scored in the weak band — visible for review, but no incident was opened.
    Weak,

    // Scored at or above the promotion threshold and opened an incident.
    Promoted,

    // Scored high enough, but an open incident already covered this signature.
    Deduplicated,

    // The signature is muted.
    Suppressed
}
