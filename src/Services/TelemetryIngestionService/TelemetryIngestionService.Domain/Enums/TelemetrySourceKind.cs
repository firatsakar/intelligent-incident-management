using System.Text.Json.Serialization;

namespace TelemetryIngestionService.Domain.Enums;

// Where a source's logs come from. Seq is pulled: a connector polls it. Otlp is pushed: the
// customer's collector sends to us in the one standard wire format, which is the answer to every
// log store we will never write a connector for.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TelemetrySourceKind
{
    Seq,
    Otlp,
}

public static class TelemetrySourceKinds
{
    /// <summary>
    /// Kinds that send to us rather than being polled. They have no connector, the poller never
    /// looks at them, and what proves one works is that data has arrived.
    /// </summary>
    /// <remarks>An array rather than a method, so a query can ask it and EF can translate the question.</remarks>
    public static readonly TelemetrySourceKind[] Pushed = [TelemetrySourceKind.Otlp];

    public static bool IsPushed(this TelemetrySourceKind kind) => Pushed.Contains(kind);
}
