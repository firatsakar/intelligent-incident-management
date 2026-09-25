namespace MonitoredShop;

public static class ShopOptions
{
    // The value the Service property carries into every log event. The telemetry connector reads
    // this property to decide which service an error belongs to.
    public const string ServiceName = "checkout-service";

    public const string DefaultSeqUrl = "http://localhost:8082";

    // Logging:Sink values.
    public const string SeqSink = "Seq";
    public const string OtlpSink = "Otlp";

    // Through the gateway, as a customer's application would reach IIM. The full path: an
    // exporter given an endpoint in code sends to exactly that address.
    public const string DefaultOtlpEndpoint = "http://localhost:5100/otlp/v1/logs";

    // OtlpController.IngestKeyHeader in TelemetryIngestionService. Repeated rather than referenced:
    // this app stands in for a customer's and references nothing of the platform.
    public const string IngestKeyHeader = "X-IIM-Ingest-Key";
}
