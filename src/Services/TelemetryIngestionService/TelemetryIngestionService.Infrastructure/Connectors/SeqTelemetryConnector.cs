using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Infrastructure.Connectors;

// Reads events from Seq's query API.
// Required settings: Url, ApiKey. Optional: Filter, ServiceProperty.
//
// Seq's events endpoint is `GET /api/events{?filter,count,afterId,clef,...}`. Asking for
// `clef=true` returns newline-delimited CLEF, which is a stable documented format and far less
// brittle than Seq's rendered JSON shape. `afterId` is the cursor: Seq's own event id, which is
// exactly the opaque token SourceCursor.Position is meant to hold.
public sealed class SeqTelemetryConnector : ITelemetrySourceConnector
{
    public const string HttpClientName = "seq-telemetry";

    private const string EventsPath = "api/events";
    private const string ApiKeyHeader = "X-Seq-ApiKey";
    private const string DefaultFilter = "@Level in ['Error','Fatal']";
    private const string DefaultServiceProperty = "Service";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SeqTelemetryConnector> _logger;

    public SeqTelemetryConnector(
        IHttpClientFactory httpClientFactory,
        ILogger<SeqTelemetryConnector> logger
    )
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public TelemetrySourceKind Kind => TelemetrySourceKind.Seq;

    public async Task<ConnectorFetchResult> FetchAsync(
        TelemetrySource source,
        SourceCursor cursor,
        int maxEvents,
        CancellationToken cancellationToken = default
    )
    {
        using var response = await SendAsync(source, cursor.Position, maxEvents, cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        var events = ParseClef(payload, source, out var lastEventId, out var lastTimestamp);

        _logger.LogInformation(
            "Fetched {Count} event(s) from telemetry source {SourceName} (after id {AfterId}).",
            events.Count,
            source.Name,
            cursor.Position ?? "<start>"
        );

        return new ConnectorFetchResult
        {
            Events = events,
            // Holding the previous position when a batch is empty keeps the cursor from rewinding
            // to the beginning of the source.
            NextPosition = lastEventId ?? cursor.Position,
            LastEventTimestamp = lastTimestamp,
        };
    }

    public async Task<ConnectorTestResult> TestAsync(
        TelemetrySource source,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var response = await SendAsync(source, afterId: null, maxEvents: 1, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return ConnectorTestResult.Failure(
                    "Seq rejected the credentials (401). The ApiKey setting needs a key with Read permission."
                );

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                return ConnectorTestResult.Failure(
                    $"Seq returned {(int)response.StatusCode} {response.ReasonPhrase}. {Truncate(body)}"
                );
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            var events = ParseClef(payload, source, out _, out _);

            return ConnectorTestResult.Success(events.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telemetry source {SourceName} failed its connection test.", source.Name);

            return ConnectorTestResult.Failure(ex.Message);
        }
    }

    private Task<HttpResponseMessage> SendAsync(
        TelemetrySource source,
        string? afterId,
        int maxEvents,
        CancellationToken cancellationToken
    )
    {
        var baseUrl = source.RequiredSetting("Url").TrimEnd('/');
        var filter = source.SettingOrDefault("Filter", DefaultFilter);

        var query = new List<string>
        {
            $"count={maxEvents}",
            "clef=true",
            $"filter={Uri.EscapeDataString(filter)}",
        };

        if (!string.IsNullOrWhiteSpace(afterId))
            query.Add($"afterId={Uri.EscapeDataString(afterId)}");

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl}/{EventsPath}?{string.Join('&', query)}"
        );

        var apiKey = source.OptionalSetting("ApiKey");

        if (apiKey is not null)
            request.Headers.TryAddWithoutValidation(ApiKeyHeader, apiKey);

        var client = _httpClientFactory.CreateClient(HttpClientName);

        return client.SendAsync(request, cancellationToken);
    }

    // CLEF is newline-delimited JSON: @t timestamp, @l level, @m rendered message, @mt template,
    // @x exception, @i event id, everything else a property.
    private IReadOnlyList<RawLogEvent> ParseClef(
        string payload,
        TelemetrySource source,
        out string? lastEventId,
        out DateTime? lastTimestamp
    )
    {
        var serviceProperty = source.SettingOrDefault("ServiceProperty", DefaultServiceProperty);
        var events = new List<RawLogEvent>();

        lastEventId = null;
        lastTimestamp = null;

        foreach (var line in payload.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
                continue;

            try
            {
                using var document = JsonDocument.Parse(trimmed);
                var root = document.RootElement;

                var timestamp = ReadTimestamp(root);

                events.Add(
                    new RawLogEvent
                    {
                        SourceEventId = ReadString(root, "@i"),
                        Timestamp = timestamp,
                        Severity = ParseSeverity(ReadString(root, "@l")),
                        Service = ReadString(root, serviceProperty) ?? source.Name,
                        Message = ReadString(root, "@m") ?? ReadString(root, "@mt") ?? string.Empty,
                        ExceptionType = ExtractExceptionType(ReadString(root, "@x")),
                        StackTrace = ReadString(root, "@x"),
                    }
                );

                lastEventId = ReadString(root, "@i") ?? lastEventId;

                if (!lastTimestamp.HasValue || timestamp > lastTimestamp)
                    lastTimestamp = timestamp;
            }
            catch (JsonException ex)
            {
                // One malformed line must not discard the rest of the batch.
                _logger.LogWarning(
                    ex,
                    "Skipped an unparsable event from telemetry source {SourceName}.",
                    source.Name
                );
            }
        }

        return events;
    }

    private static DateTime ReadTimestamp(JsonElement root)
    {
        var raw = ReadString(root, "@t");

        return DateTime.TryParse(
            raw,
            null,
            System.Globalization.DateTimeStyles.AdjustToUniversal
                | System.Globalization.DateTimeStyles.AssumeUniversal,
            out var parsed
        )
            ? parsed
            : DateTime.UtcNow;
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => value.ToString(),
        };
    }

    // CLEF omits @l for Information — that is the documented default, not a missing value.
    private static LogSeverity ParseSeverity(string? level)
    {
        return Enum.TryParse<LogSeverity>(level, ignoreCase: true, out var parsed)
            ? parsed
            : LogSeverity.Information;
    }

    // A .NET exception string starts with "Namespace.TypeName: message", so the type is everything
    // before the first colon on the first line.
    private static string? ExtractExceptionType(string? exception)
    {
        if (string.IsNullOrWhiteSpace(exception))
            return null;

        var firstLine = exception.Split('\n', 2)[0].Trim();
        var colon = firstLine.IndexOf(':');
        var candidate = colon > 0 ? firstLine[..colon] : firstLine;

        return candidate.Length is > 0 and <= 512 ? candidate : null;
    }

    private static string Truncate(string body)
    {
        const int limit = 512;

        return body.Length <= limit ? body : body[..limit];
    }
}
