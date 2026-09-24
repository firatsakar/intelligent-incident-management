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
                        NpgsqlActivitySource
                    )
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
}
