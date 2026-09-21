using System.Text.Json.Serialization;

namespace TelemetryIngestionService.Domain.Enums;

// Named LogSeverity rather than LogLevel to avoid colliding with
// Microsoft.Extensions.Logging.LogLevel everywhere this is used. Order is ascending severity.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LogSeverity
{
    Verbose,
    Debug,
    Information,
    Warning,
    Error,
    Fatal
}
