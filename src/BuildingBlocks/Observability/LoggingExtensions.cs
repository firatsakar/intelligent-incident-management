using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace BuildingBlocks.Observability;

public static class LoggingExtensions
{
    public const string SeqUrlConfigurationKey = "Seq:Url";

    private const string DefaultSeqUrl = "http://localhost:8081";

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
