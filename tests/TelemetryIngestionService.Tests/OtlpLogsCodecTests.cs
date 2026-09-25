using System.Text;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using TelemetryIngestionService.Domain.Enums;
using TelemetryIngestionService.Infrastructure.Otlp;
using OtlpLogRecord = OpenTelemetry.Proto.Logs.V1.LogRecord;

namespace TelemetryIngestionService.Tests;

// OTLP is the wire format every shipper speaks, so the mapping follows the semantic conventions
// rather than any one sender. These pin the conventions and the two senders that shape the
// message handling: the .NET SDK and Serilog's sink.
public sealed class OtlpLogsCodecTests
{
    private static readonly DateTime ReceivedAt = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime HappenedAt = new(2026, 9, 24, 11, 59, 30, DateTimeKind.Utc);

    private static KeyValue Attribute(string key, string value) =>
        new() { Key = key, Value = new AnyValue { StringValue = value } };

    private static KeyValue Attribute(string key, long value) =>
        new() { Key = key, Value = new AnyValue { IntValue = value } };

    private static ulong Nanos(DateTime at) => (ulong)(at - DateTime.UnixEpoch).Ticks * 100;

    private static ExportLogsServiceRequest Request(string? service, params OtlpLogRecord[] records)
    {
        var resource = new Resource();

        if (service is not null)
            resource.Attributes.Add(Attribute("service.name", service));

        var scope = new ScopeLogs();
        scope.LogRecords.AddRange(records);

        var resourceLogs = new ResourceLogs { Resource = resource };
        resourceLogs.ScopeLogs.Add(scope);

        var request = new ExportLogsServiceRequest();
        request.ResourceLogs.Add(resourceLogs);

        return request;
    }

    // What the .NET SDK sends by default: the template as the body, the values as attributes.
    private static OtlpLogRecord DotNetCheckoutFailure(long orderId = 481516)
    {
        var record = new OtlpLogRecord
        {
            TimeUnixNano = Nanos(HappenedAt),
            SeverityNumber = SeverityNumber.Error,
            SeverityText = "Error",
            Body = new AnyValue { StringValue = "Checkout failed for order {OrderId}" },
        };

        record.Attributes.Add(Attribute("OrderId", orderId));
        record.Attributes.Add(Attribute("{OriginalFormat}", "Checkout failed for order {OrderId}"));
        record.Attributes.Add(Attribute("exception.type", "System.InvalidOperationException"));
        record.Attributes.Add(Attribute("exception.stacktrace", "System.InvalidOperationException: pool exhausted\n   at Checkout.Place()"));

        return record;
    }

    [Fact]
    public void TheSemanticConventionsMapOntoOurVocabulary()
    {
        var batch = OtlpLogsCodec.Map(Request("checkout-service", DotNetCheckoutFailure()), ReceivedAt);

        var mapped = Assert.Single(batch.Events);
        Assert.Equal("checkout-service", mapped.Service);
        Assert.Equal(LogSeverity.Error, mapped.Severity);
        Assert.Equal(HappenedAt, mapped.Timestamp);
        Assert.Equal("System.InvalidOperationException", mapped.ExceptionType);
        Assert.StartsWith("System.InvalidOperationException: pool exhausted", mapped.StackTrace);
        Assert.Equal(0, batch.Rejected);
    }

    [Fact]
    public void ATemplateBodyIsRenderedFromItsAttributesAndKeptAsTheTemplate()
    {
        // The evidence should say which order; the fingerprint should not care.
        var mapped = Assert.Single(OtlpLogsCodec.Map(Request("checkout-service", DotNetCheckoutFailure(4471)), ReceivedAt).Events);

        Assert.Equal("Checkout failed for order 4471", mapped.Message);
        Assert.Equal("Checkout failed for order {OrderId}", mapped.MessageTemplate);
    }

    [Fact]
    public void ARenderedBodyIsKeptAsItCame()
    {
        // Serilog's sink: rendered text in the body, the template in an attribute of its own.
        var record = new OtlpLogRecord
        {
            TimeUnixNano = Nanos(HappenedAt),
            SeverityNumber = SeverityNumber.Error,
            Body = new AnyValue { StringValue = "Payment gateway did not respond within 30s for cart 7" },
        };
        record.Attributes.Add(Attribute("message_template.text", "Payment gateway did not respond within {Seconds}s for cart {CartId}"));

        var mapped = Assert.Single(OtlpLogsCodec.Map(Request("payments", record), ReceivedAt).Events);

        Assert.Equal("Payment gateway did not respond within 30s for cart 7", mapped.Message);
        Assert.Equal("Payment gateway did not respond within {Seconds}s for cart {CartId}", mapped.MessageTemplate);
    }

    [Theory]
    [InlineData(SeverityNumber.Trace2, LogSeverity.Verbose)]
    [InlineData(SeverityNumber.Debug, LogSeverity.Debug)]
    [InlineData(SeverityNumber.Info4, LogSeverity.Information)]
    [InlineData(SeverityNumber.Warn, LogSeverity.Warning)]
    [InlineData(SeverityNumber.Error3, LogSeverity.Error)]
    [InlineData(SeverityNumber.Fatal, LogSeverity.Fatal)]
    public void SeverityFollowsTheNumber(SeverityNumber number, LogSeverity expected)
    {
        var record = new OtlpLogRecord { SeverityNumber = number, Body = new AnyValue { StringValue = "x" } };

        Assert.Equal(expected, Assert.Single(OtlpLogsCodec.Map(Request("s", record), ReceivedAt).Events).Severity);
    }

    [Theory]
    [InlineData("CRITICAL", LogSeverity.Fatal)]
    [InlineData("warn", LogSeverity.Warning)]
    [InlineData("error", LogSeverity.Error)]
    [InlineData("something else", LogSeverity.Information)]
    public void WithoutANumberTheTextDecides(string text, LogSeverity expected)
    {
        // Fluent Bit and Vector often forward a level string and leave the number unset.
        var record = new OtlpLogRecord { SeverityText = text, Body = new AnyValue { StringValue = "x" } };

        Assert.Equal(expected, Assert.Single(OtlpLogsCodec.Map(Request("s", record), ReceivedAt).Events).Severity);
    }

    [Fact]
    public void AMissingServiceNameIsSaidRatherThanGuessed()
    {
        var mapped = Assert.Single(OtlpLogsCodec.Map(Request(null, DotNetCheckoutFailure()), ReceivedAt).Events);

        Assert.Equal("unknown_service", mapped.Service);
    }

    [Fact]
    public void WithoutAnEventTimeTheObservedTimeAndThenReceiptStandIn()
    {
        var observed = new OtlpLogRecord { ObservedTimeUnixNano = Nanos(HappenedAt), Body = new AnyValue { StringValue = "x" } };
        var untimed = new OtlpLogRecord { Body = new AnyValue { StringValue = "y" } };

        var events = OtlpLogsCodec.Map(Request("s", observed, untimed), ReceivedAt).Events;

        Assert.Equal(HappenedAt, events[0].Timestamp);
        Assert.Equal(ReceivedAt, events[1].Timestamp);
    }

    [Fact]
    public void ARecordWithNothingToSayIsRejectedAndCounted()
    {
        var empty = new OtlpLogRecord { SeverityNumber = SeverityNumber.Error };

        var batch = OtlpLogsCodec.Map(Request("s", empty, DotNetCheckoutFailure()), ReceivedAt);

        Assert.Single(batch.Events);
        Assert.Equal(1, batch.Rejected);
    }

    [Fact]
    public void AResentRecordHasTheSameIdAndTwoDifferentOnesDoNot()
    {
        // A sender that retries a batch it never heard back about resends the same bytes; the
        // unique index on (source, event id) is what keeps them from counting twice.
        var first = OtlpLogsCodec.Map(Request("s", DotNetCheckoutFailure(1)), ReceivedAt).Events.Single();
        var resent = OtlpLogsCodec.Map(Request("s", DotNetCheckoutFailure(1)), ReceivedAt).Events.Single();
        var other = OtlpLogsCodec.Map(Request("s", DotNetCheckoutFailure(2)), ReceivedAt).Events.Single();

        Assert.Equal(first.SourceEventId, resent.SourceEventId);
        Assert.NotEqual(first.SourceEventId, other.SourceEventId);
    }

    [Fact]
    public void ProtobufAndJsonCarryTheSameRecord()
    {
        var request = Request("checkout-service", DotNetCheckoutFailure());

        var fromProtobuf = OtlpLogsCodec.Parse(request.ToByteArray(), OtlpEncoding.Protobuf);
        var fromJson = OtlpLogsCodec.Parse(Encoding.UTF8.GetBytes(JsonFormatter.Default.Format(request)), OtlpEncoding.Json);

        Assert.Equal(
            OtlpLogsCodec.Map(fromProtobuf, ReceivedAt).Events.Single(),
            OtlpLogsCodec.Map(fromJson, ReceivedAt).Events.Single()
        );
    }

    [Fact]
    public void JsonAsTheSpecWritesItIsAccepted()
    {
        // The OTLP/JSON spec's own shape: camelCase keys, the severity as a number, nanoseconds as
        // a string, trace ids in hex. Hand-written rather than produced by our own formatter.
        const string json = """
            {"resourceLogs":[{"resource":{"attributes":[{"key":"service.name","value":{"stringValue":"payments"}}]},
              "scopeLogs":[{"logRecords":[{"timeUnixNano":"1727179170000000000","severityNumber":17,
                "body":{"stringValue":"Payment declined"},"traceId":"5b8efff798038103d269b633813fc60c",
                "spanId":"eee19b7ec3c1b174","attributes":[{"key":"exception.type","value":{"stringValue":"DeclinedException"}}]}]}]}]}
            """;

        var mapped = OtlpLogsCodec.Map(OtlpLogsCodec.Parse(Encoding.UTF8.GetBytes(json), OtlpEncoding.Json), ReceivedAt).Events.Single();

        Assert.Equal("payments", mapped.Service);
        Assert.Equal(LogSeverity.Error, mapped.Severity);
        Assert.Equal("Payment declined", mapped.Message);
        Assert.Equal("DeclinedException", mapped.ExceptionType);
        Assert.Equal(DateTime.UnixEpoch.AddSeconds(1727179170), mapped.Timestamp);
    }

    [Theory]
    [InlineData("application/x-protobuf", OtlpEncoding.Protobuf)]
    [InlineData("application/json; charset=utf-8", OtlpEncoding.Json)]
    [InlineData("text/plain", null)]
    [InlineData(null, null)]
    public void TheContentTypeChoosesTheEncoding(string? contentType, OtlpEncoding? expected)
    {
        Assert.Equal(expected, OtlpLogsCodec.EncodingOf(contentType));
    }

    [Fact]
    public void RejectionsAreReportedAsPartialSuccess()
    {
        var response = ExportLogsServiceResponse.Parser.ParseFrom(OtlpLogsCodec.Response(OtlpEncoding.Protobuf, rejected: 3));

        Assert.Equal(3, response.PartialSuccess.RejectedLogRecords);
    }

    [Fact]
    public void AFullyAcceptedBatchReportsNothing()
    {
        var response = ExportLogsServiceResponse.Parser.ParseFrom(OtlpLogsCodec.Response(OtlpEncoding.Protobuf, rejected: 0));

        Assert.Null(response.PartialSuccess);
    }
}
