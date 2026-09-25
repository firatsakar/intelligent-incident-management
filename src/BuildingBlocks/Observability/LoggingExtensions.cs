using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace BuildingBlocks.Observability;

public static class LoggingExtensions
{
    public const string SeqUrlConfigurationKey = "Seq:Url";

    // Shared with TracingExtensions: logs and traces go to the same Seq, which is what lets a log
    // event open the trace it was written inside.
    internal const string DefaultSeqUrl = "http://localhost:8081";

    // Every service logs the same way: structured, to the console for whoever is watching a
    // terminal and to Seq for everything else. Stamping the service name here rather than at each
    // call site is what makes a Seq query like "Service = 'agent-orchestrator'" possible.
    //
    // This is the platform's *own* observability. It is not the telemetry the detector consumes —
    // that comes from a source the customer configures, which is a different Seq entirely.
    public static IHostBuilder UsePlatformLogging(this IHostBuilder host, string serviceName)
    {
        return host.UseSerilog(
            (context, configuration) =>
                configuration
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                    // Same judgement as the two above: the gateway logs two Information lines per
                    // proxied request, and every call the console makes is a proxied request. The
                    // services that do not reference YARP are unaffected by this line.
                    .MinimumLevel.Override("Yarp", LogEventLevel.Warning)
                    // The trace exporter posts to Seq every five seconds through IHttpClientFactory,
                    // which logs four Information lines per post — into the same Seq, about the
                    // act of sending it spans. A failed export still surfaces as a warning.
                    .MinimumLevel.Override(
                        "System.Net.Http.HttpClient.OtlpTraceExporter",
                        LogEventLevel.Warning
                    )
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Service", serviceName)
                    .WriteTo.Console()
                    .WriteTo.Seq(
                        context.Configuration[SeqUrlConfigurationKey] ?? DefaultSeqUrl,
                        // A short flush keeps a terminal and Seq roughly in step while
                        // developing; the default interval makes logs feel lost.
                        period: TimeSpan.FromSeconds(2)
                    )
        );
    }
}
