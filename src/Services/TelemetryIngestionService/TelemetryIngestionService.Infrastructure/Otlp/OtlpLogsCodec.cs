using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Enums;
using OtlpLogRecord = OpenTelemetry.Proto.Logs.V1.LogRecord;

namespace TelemetryIngestionService.Infrastructure.Otlp;

public enum OtlpEncoding
{
    Protobuf,
    Json,
}

/// <summary>What one OTLP export request turned into, and what it could not.</summary>
public sealed record OtlpDecodedBatch(IReadOnlyList<RawLogEvent> Events, int Rejected);

/// <summary>
/// OTLP/HTTP logs: reading an export request, mapping its records onto our vocabulary, and writing
/// the response the sender expects.
/// </summary>
/// <remarks>
/// <para>
/// The mapping follows the OpenTelemetry semantic conventions rather than any one sender, which is
/// the point of accepting OTLP at all: the service is the resource's <c>service.name</c>, the
/// exception is <c>exception.type</c> and <c>exception.stacktrace</c>, and severity is the 1–24
/// number every SDK sets.
/// </para>
/// <para>
/// Two senders shape the message handling. The .NET SDK sends the <em>template</em> as the body and
/// the values as attributes, and spends its <c>{OriginalFormat}</c> attribute on that body rather
/// than sending it — so a body whose placeholders name attributes is taken to be the template and
/// rendered from them, because a person reading the evidence wants the order id. (Established by
/// the acceptance run: the unit tests had assumed the attribute arrived, and the evidence screen
/// showed "{OrderId}".) Serilog's sink sends the rendered text and the template as an attribute.
/// Either way the template is kept, since it is the best normalisation there is.
/// </para>
/// </remarks>
public static partial class OtlpLogsCodec
{
    public const string ProtobufContentType = "application/x-protobuf";
    public const string JsonContentType = "application/json";

    private const string UnknownService = "unknown_service";

    // Where the two common senders put the message template.
    private static readonly string[] TemplateAttributes = ["{OriginalFormat}", "message_template.text"];

    private static readonly JsonParser Json = new(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));

    public static OtlpEncoding? EncodingOf(string? contentType)
    {
        var mediaType = contentType?.Split(';', 2)[0].Trim();

        return mediaType?.ToLowerInvariant() switch
        {
            ProtobufContentType => OtlpEncoding.Protobuf,
            JsonContentType => OtlpEncoding.Json,
            _ => null,
        };
    }

    /// <summary>Throws <see cref="InvalidProtocolBufferException"/> or <see cref="InvalidJsonException"/> on a malformed body.</summary>
    public static ExportLogsServiceRequest Parse(byte[] body, OtlpEncoding encoding) =>
        encoding switch
        {
            OtlpEncoding.Protobuf => ExportLogsServiceRequest.Parser.ParseFrom(body),
            _ => Json.Parse<ExportLogsServiceRequest>(Encoding.UTF8.GetString(body)),
        };

    public static OtlpDecodedBatch Map(ExportLogsServiceRequest request, DateTime receivedAt)
    {
        var events = new List<RawLogEvent>();
        var rejected = 0;

        foreach (var resourceLogs in request.ResourceLogs)
        {
            var service = StringAttribute(resourceLogs.Resource?.Attributes, "service.name") ?? UnknownService;

            foreach (var record in resourceLogs.ScopeLogs.SelectMany(scope => scope.LogRecords))
            {
                if (TryMap(service, record, receivedAt) is { } mapped)
                    events.Add(mapped);
                else
                    rejected++;
            }
        }

        return new OtlpDecodedBatch(events, rejected);
    }

    public static byte[] Response(OtlpEncoding encoding, int rejected)
    {
        var response = new ExportLogsServiceResponse();

        // Partial success is how OTLP says "accepted, except these" — and the sender logs the
        // message, which is the only place the customer will ever read it.
        if (rejected > 0)
            response.PartialSuccess = new ExportLogsPartialSuccess
            {
                RejectedLogRecords = rejected,
                ErrorMessage = "Log records with neither a body nor an exception message carry nothing to store.",
            };

        return encoding == OtlpEncoding.Protobuf
            ? response.ToByteArray()
            : Encoding.UTF8.GetBytes(JsonFormatter.Default.Format(response));
    }

    private static RawLogEvent? TryMap(string service, OtlpLogRecord record, DateTime receivedAt)
    {
        var body = record.Body is null ? null : Render(record.Body);
        var template =
            TemplateAttributes
                .Select(key => StringAttribute(record.Attributes, key))
                .FirstOrDefault(value => !string.IsNullOrEmpty(value))
            ?? (IsTemplate(body, record.Attributes) ? body : null);

        // The .NET SDK's default: the body is the template itself, the values are attributes.
        var message = template is not null && (string.IsNullOrEmpty(body) || body == template)
            ? RenderTemplate(template, record.Attributes)
            : body;

        if (string.IsNullOrWhiteSpace(message))
            message = StringAttribute(record.Attributes, "exception.message");

        if (string.IsNullOrWhiteSpace(message))
            return null;

        return new RawLogEvent
        {
            SourceEventId = EventId(service, record),
            Timestamp = TimestampOf(record, receivedAt),
            Severity = SeverityOf(record),
            Service = service,
            Message = message,
            MessageTemplate = template,
            ExceptionType = StringAttribute(record.Attributes, "exception.type"),
            StackTrace = StringAttribute(record.Attributes, "exception.stacktrace"),
        };
    }

    // OTLP records carry no id. The record's own bytes stand in for one: a sender retrying a batch
    // it never got an answer for sends the same bytes, and the unique index on (source, event id)
    // drops the repeat. Two genuinely separate events differ in time or attributes, so in bytes.
    private static string EventId(string service, OtlpLogRecord record)
    {
        var bytes = Encoding.UTF8.GetBytes(service + "\0").Concat(record.ToByteArray()).ToArray();

        return "otlp-" + Convert.ToHexStringLower(SHA256.HashData(bytes).AsSpan(0, 16));
    }

    private static DateTime TimestampOf(OtlpLogRecord record, DateTime receivedAt)
    {
        // When it happened if the sender knows, when the collector saw it if not, and when we
        // received it as the last resort — a record without a time is still a record.
        var nanos = record.TimeUnixNano != 0 ? record.TimeUnixNano : record.ObservedTimeUnixNano;

        if (nanos == 0)
            return receivedAt;

        var ticks = nanos / 100;

        return ticks > (ulong)(DateTime.MaxValue - DateTime.UnixEpoch).Ticks
            ? receivedAt
            : DateTime.UnixEpoch.AddTicks((long)ticks);
    }

    private static LogSeverity SeverityOf(OtlpLogRecord record)
    {
        var number = (int)record.SeverityNumber;

        if (number is >= 1 and <= 24)
            return number switch
            {
                <= 4 => LogSeverity.Verbose,
                <= 8 => LogSeverity.Debug,
                <= 12 => LogSeverity.Information,
                <= 16 => LogSeverity.Warning,
                <= 20 => LogSeverity.Error,
                _ => LogSeverity.Fatal,
            };

        // Unspecified number: the text, which is the source's own name for the level.
        return record.SeverityText?.Trim().ToLowerInvariant() switch
        {
            "trace" or "verbose" => LogSeverity.Verbose,
            "debug" => LogSeverity.Debug,
            "warn" or "warning" => LogSeverity.Warning,
            "error" or "err" => LogSeverity.Error,
            "fatal" or "critical" or "crit" or "emergency" or "alert" => LogSeverity.Fatal,
            _ => LogSeverity.Information,
        };
    }

    private static string? StringAttribute(IEnumerable<KeyValue>? attributes, string key)
    {
        var value = attributes?.FirstOrDefault(attribute => attribute.Key == key)?.Value;

        return value is null ? null : Render(value);
    }

    // A body is a template when at least one of its placeholders names an attribute the record
    // carries. Braces alone are not enough — "{}" and JSON bodies have them too.
    private static bool IsTemplate(string? body, IEnumerable<KeyValue> attributes)
    {
        if (string.IsNullOrEmpty(body) || !body.Contains('{'))
            return false;

        var keys = attributes.Select(attribute => attribute.Key).ToHashSet(StringComparer.Ordinal);

        return Placeholder().Matches(body).Any(match => keys.Contains(match.Groups["name"].Value));
    }

    private static string RenderTemplate(string template, IEnumerable<KeyValue> attributes)
    {
        var values = attributes
            .GroupBy(attribute => attribute.Key)
            .ToDictionary(group => group.Key, group => Render(group.First().Value));

        // "{OrderId}", "{@Order}", "{Elapsed:0.00}" — a placeholder with no matching attribute is
        // left as written rather than dropped, so the gap is visible.
        return Placeholder().Replace(
            template,
            match => values.TryGetValue(match.Groups["name"].Value, out var value) ? value : match.Value
        );
    }

    private static string Render(AnyValue? value) =>
        value?.ValueCase switch
        {
            AnyValue.ValueOneofCase.StringValue => value.StringValue,
            AnyValue.ValueOneofCase.BoolValue => value.BoolValue ? "true" : "false",
            AnyValue.ValueOneofCase.IntValue => value.IntValue.ToString(CultureInfo.InvariantCulture),
            AnyValue.ValueOneofCase.DoubleValue => value.DoubleValue.ToString(CultureInfo.InvariantCulture),
            AnyValue.ValueOneofCase.ArrayValue => "[" + string.Join(", ", value.ArrayValue.Values.Select(Render)) + "]",
            AnyValue.ValueOneofCase.KvlistValue => "{" + string.Join(", ", value.KvlistValue.Values.Select(pair => $"{pair.Key}={Render(pair.Value)}")) + "}",
            AnyValue.ValueOneofCase.BytesValue => Convert.ToBase64String(value.BytesValue.ToByteArray()),
            _ => string.Empty,
        };

    [GeneratedRegex(@"\{[@$]?(?<name>[A-Za-z0-9_.]+)(,[^}:]*)?(:[^}]*)?\}")]
    private static partial Regex Placeholder();
}
