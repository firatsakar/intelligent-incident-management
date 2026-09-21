using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Infrastructure.Connectors;

// Reads events from Seq's query API.
// Required settings: Url. Optional: ApiKey, Filter, ServiceProperty, InitialLookbackMinutes.
//
// Three things about Seq's API shaped this implementation, all established by querying a live
// instance rather than assumed:
//
// 1. CLEF output (clef=true) is tempting but unusable as a cursor source: its `@i` field is the
//    *event type hash*, identical for every event sharing a message template, not a unique id.
//    The rendered shape is the one that carries a real per-event `Id`.
// 2. Results come back newest first, so `afterId` pages backwards in time. It is a pagination
//    token, not a tail cursor. Tailing is done with `fromDateUtc`, which is inclusive.
// 3. Because `fromDateUtc` is inclusive, the boundary event is re-fetched on every poll. That is
//    deliberate: it guarantees no gap, and the unique index on (source, event id) discards the
//    duplicate.
public sealed class SeqTelemetryConnector : ITelemetrySourceConnector
{
    public const string HttpClientName = "seq-telemetry";

    private const string EventsPath = "api/events";
    private const string ApiKeyHeader = "X-Seq-ApiKey";
    private const string DefaultFilter = "@Level in ['Error','Fatal']";
    private const string DefaultServiceProperty = "Service";
    private const int DefaultInitialLookbackMinutes = 15;

    // Bounds one poll. A window holding more than this is a firehose; the warning says so rather
    // than the connector quietly spinning.
    private const int MaxPagesPerPoll = 10;

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
        // On a source's first poll, start from a short lookback rather than the beginning of the
        // store. Ingesting a year of history would open incidents for errors nobody is acting on.
        var from = cursor.LastEventTimestamp ?? DateTime.UtcNow.AddMinutes(-ResolveLookback(source));

        var collected = new List<RawLogEvent>();
        string? afterId = null;

        for (var page = 0; page < MaxPagesPerPoll; page++)
        {
            using var response = await SendAsync(source, from, afterId, maxEvents, cancellationToken);

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            var events = Parse(payload, source, out var oldestId);

            collected.AddRange(events);

            // A short page means the window is exhausted. A full one means there may be older
            // events still inside it, reachable by paging backwards from the oldest we just saw.
            if (events.Count < maxEvents || oldestId is null)
                break;

            afterId = oldestId;

            if (page == MaxPagesPerPoll - 1)
            {
                _logger.LogWarning(
                    "Telemetry source {SourceName} still had events after {PageCount} pages; the rest will be picked up on the next poll.",
                    source.Name,
                    MaxPagesPerPoll
                );
            }
        }

        // Seq hands them back newest first; downstream wants oldest first so occurrences are
        // recorded in the order they happened.
        var ordered = collected.OrderBy(x => x.Timestamp).ToList();

        _logger.LogInformation(
            "Fetched {Count} event(s) from telemetry source {SourceName} (from {From:O}).",
            ordered.Count,
            source.Name,
            from
        );

        return new ConnectorFetchResult
        {
            Events = ordered,
            // Holding the previous position when nothing new arrived keeps the cursor from
            // rewinding to the start of the source.
            NextPosition = ordered.Count > 0 ? ordered[^1].SourceEventId : cursor.Position,
            LastEventTimestamp = ordered.Count > 0 ? ordered[^1].Timestamp : cursor.LastEventTimestamp,
        };
    }

    public async Task<ConnectorTestResult> TestAsync(
        TelemetrySource source,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var response = await SendAsync(
                source,
                from: DateTime.UtcNow.AddMinutes(-ResolveLookback(source)),
                afterId: null,
                maxEvents: 1,
                cancellationToken
            );

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return ConnectorTestResult.Failure(
                    "Seq rejected the credentials (401). The ApiKey setting needs a key with Read permission."
                );

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

                return ConnectorTestResult.Failure(
                    $"Seq returned {(int)response.StatusCode} {response.ReasonPhrase}. {Truncate(errorBody)}"
                );
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            return ConnectorTestResult.Success(Parse(payload, source, out _).Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telemetry source {SourceName} failed its connection test.", source.Name);

            return ConnectorTestResult.Failure(ex.Message);
        }
    }

    private Task<HttpResponseMessage> SendAsync(
        TelemetrySource source,
        DateTime from,
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
            "render=true",
            $"filter={Uri.EscapeDataString(filter)}",
            $"fromDateUtc={Uri.EscapeDataString(from.ToString("O", CultureInfo.InvariantCulture))}",
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

        return _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
    }

    private IReadOnlyList<RawLogEvent> Parse(
        string payload,
        TelemetrySource source,
        out string? oldestId
    )
    {
        var serviceProperty = source.SettingOrDefault("ServiceProperty", DefaultServiceProperty);
        var events = new List<RawLogEvent>();

        oldestId = null;

        using var document = JsonDocument.Parse(payload);

        foreach (var element in document.RootElement.EnumerateArray())
        {
            var exception = ReadString(element, "Exception");
            var id = ReadString(element, "Id");

            events.Add(
                new RawLogEvent
                {
                    SourceEventId = id,
                    Timestamp = ReadTimestamp(element),
                    Severity = ParseSeverity(ReadString(element, "Level")),
                    Service = ReadProperty(element, serviceProperty) ?? source.Name,
                    Message = ReadString(element, "RenderedMessage") ?? string.Empty,
                    MessageTemplate = ReadTemplate(element),
                    ExceptionType = ExtractExceptionType(exception),
                    StackTrace = exception,
                }
            );

            // Results are newest first, so the last one seen is the oldest of the page.
            oldestId = id ?? oldestId;
        }

        return events;
    }

    // Rebuilds "Checkout failed for order {OrderId}" from Seq's tokenised template.
    private static string? ReadTemplate(JsonElement element)
    {
        if (!element.TryGetProperty("MessageTemplateTokens", out var tokens)
            || tokens.ValueKind != JsonValueKind.Array)
            return null;

        var builder = new StringBuilder();

        foreach (var token in tokens.EnumerateArray())
        {
            if (token.TryGetProperty("Text", out var text))
                builder.Append(text.GetString());
            else if (token.TryGetProperty("PropertyName", out var name))
                builder.Append('{').Append(name.GetString()).Append('}');
        }

        return builder.Length > 0 ? builder.ToString() : null;
    }

    // Properties arrive as an array of {Name, Value} rather than an object.
    private static string? ReadProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty("Properties", out var properties)
            || properties.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var property in properties.EnumerateArray())
        {
            if (!property.TryGetProperty("Name", out var name)
                || !string.Equals(name.GetString(), propertyName, StringComparison.OrdinalIgnoreCase))
                continue;

            return property.TryGetProperty("Value", out var value) ? ValueToString(value) : null;
        }

        return null;
    }

    private static DateTime ReadTimestamp(JsonElement element)
    {
        return DateTime.TryParse(
            ReadString(element, "Timestamp"),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var parsed
        )
            ? parsed
            : DateTime.UtcNow;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) ? ValueToString(value) : null;
    }

    private static string? ValueToString(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => value.ToString(),
        };
    }

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

    private static int ResolveLookback(TelemetrySource source)
    {
        var configured = source.OptionalSetting("InitialLookbackMinutes");

        return configured is not null && int.TryParse(configured, out var minutes) && minutes > 0
            ? minutes
            : DefaultInitialLookbackMinutes;
    }

    private static string Truncate(string body)
    {
        const int limit = 512;

        return body.Length <= limit ? body : body[..limit];
    }
}
