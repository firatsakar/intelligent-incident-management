using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BuildingBlocks.Observability;

public static class TracingExtensions
{
    // Npgsql has emitted its own activities since v6; listening to the source is all its
    // instrumentation package would do, and naming it here keeps the Npgsql version out of this
    // project.
    private const string NpgsqlActivitySource = "Npgsql";

    // RabbitMQ.Client 7 traces itself: a publish span that writes W3C trace context into the
    // message headers, and a deliver span, parented on that context, that the consumer's handler
    // runs inside. Listening is what switches it on — with no listener the client neither starts
    // the spans nor touches the headers — so these two names are the whole of the bus's
    // propagation.
    private const string RabbitMqPublisherActivitySource = "RabbitMQ.Client.Publisher";
    private const string RabbitMqSubscriberActivitySource = "RabbitMQ.Client.Subscriber";

    // OutboxDispatcher.ActivitySourceName, spelled out because this project does not reference the
    // outbox and should not start to for the sake of one string.
    private const string OutboxActivitySource = "BuildingBlocks.Outbox";

    private const string OtlpTracesPath = "ingest/otlp/v1/traces";

    /// <summary>
    /// The platform's own distributed tracing: every inbound request, outbound HTTP call and SQL
    /// command becomes a span, exported to the same Seq the logs go to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Seq rather than a dedicated trace store because the logs are already there. Serilog stamps
    /// each event with the trace and span it was written inside, so in one UI a log line opens its
    /// trace and a span lists its log lines. A separate tool would hold the same ids and make a
    /// person carry them across by hand.
    /// </para>
    /// <para>
    /// Like <see cref="LoggingExtensions.UsePlatformLogging"/>, this watches the platform, not the
    /// customer. The telemetry the detector consumes comes from a source the customer configures
    /// and never passes through here.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddPlatformTracing(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName
    )
    {
        var seq = new Uri(
            (configuration[LoggingExtensions.SeqUrlConfigurationKey] ?? LoggingExtensions.DefaultSeqUrl)
                .TrimEnd('/') + "/"
        );

        services
            .AddOpenTelemetry()
            // The same name Serilog stamps as "Service", so a Seq query and a trace agree on who
            // is speaking.
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
                tracing
                    .AddSource(
                        TelemetryConstants.ActivitySources.IncidentService,
                        TelemetryConstants.ActivitySources.NotificationService,
                        TelemetryConstants.ActivitySources.TelemetryIngestionService,
                        TelemetryConstants.ActivitySources.AgentOrchestrator,
                        NpgsqlActivitySource,
                        RabbitMqPublisherActivitySource,
                        RabbitMqSubscriberActivitySource,
                        OutboxActivitySource
                    )
                    .SetSampler(new ParentBasedSampler(new DropUnstartedClientCallsSampler()))
                    .AddAspNetCoreInstrumentation(aspNetCore =>
                    {
                        // A hub connection is one request that lives as long as the browser tab.
                        // As a span it is minutes long, contains nothing, and pushes every real
                        // request off the first screen of a trace search.
                        aspNetCore.Filter = context =>
                            !context.Request.Path.StartsWithSegments("/hubs");
                    })
                    .AddHttpClientInstrumentation(http =>
                    {
                        // The Serilog sink posts a batch to Seq every two seconds from every
                        // process. Traced, that is a span every two seconds per service about
                        // the act of logging, and every one of them lands in the same Seq.
                        http.FilterHttpRequestMessage = request =>
                            request.RequestUri is null
                            || !string.Equals(
                                request.RequestUri.Authority,
                                seq.Authority,
                                StringComparison.OrdinalIgnoreCase
                            );
                    })
                    .AddOtlpExporter(otlp =>
                    {
                        // A full path, not a base: with an endpoint set in code the exporter
                        // sends to exactly this address and appends nothing.
                        otlp.Endpoint = new Uri(seq, OtlpTracesPath);
                        otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
                    })
            );

        return services;
    }

    /// <summary>
    /// Decides only for spans with no parent, and keeps every one of them except outbound calls.
    /// </summary>
    /// <remarks>
    /// The background loops — outbox dispatchers, the telemetry poller, the cleanup jobs — query
    /// their tables every few seconds with nothing in flight. Each of those queries is a root
    /// client span: a trace consisting of one SELECT, twelve a minute from each service, burying
    /// the traces anyone would open. A client call nothing started is not worth a trace. The work
    /// those loops do that does matter opens a span of its own first, and its queries and calls
    /// nest under it rather than reaching this decision.
    /// </remarks>
    private sealed class DropUnstartedClientCallsSampler : Sampler
    {
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) =>
            new(
                samplingParameters.Kind == ActivityKind.Client
                    ? SamplingDecision.Drop
                    : SamplingDecision.RecordAndSample
            );
    }
}
