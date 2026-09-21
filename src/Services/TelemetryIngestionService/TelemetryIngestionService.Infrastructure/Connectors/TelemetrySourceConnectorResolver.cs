using Microsoft.Extensions.DependencyInjection;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Infrastructure.Connectors;

public sealed class TelemetrySourceConnectorResolver : ITelemetrySourceConnectorResolver
{
    private readonly IServiceProvider _serviceProvider;

    public TelemetrySourceConnectorResolver(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ITelemetrySourceConnector Resolve(TelemetrySourceKind kind)
    {
        return _serviceProvider.GetRequiredKeyedService<ITelemetrySourceConnector>(kind);
    }
}
