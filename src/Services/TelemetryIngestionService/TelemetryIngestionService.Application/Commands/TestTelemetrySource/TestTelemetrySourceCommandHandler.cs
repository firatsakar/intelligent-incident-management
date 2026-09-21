using MediatR;
using Microsoft.Extensions.Logging;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Exceptions;

namespace TelemetryIngestionService.Application.Commands.TestTelemetrySource;

public sealed class TestTelemetrySourceCommandHandler
    : IRequestHandler<TestTelemetrySourceCommand, ConnectorTestResult>
{
    private readonly ITelemetrySourceRepository _sources;
    private readonly ITelemetrySourceConnectorResolver _connectors;
    private readonly ILogger<TestTelemetrySourceCommandHandler> _logger;

    public TestTelemetrySourceCommandHandler(
        ITelemetrySourceRepository sources,
        ITelemetrySourceConnectorResolver connectors,
        ILogger<TestTelemetrySourceCommandHandler> logger
    )
    {
        _sources = sources;
        _connectors = connectors;
        _logger = logger;
    }

    public async Task<ConnectorTestResult> Handle(
        TestTelemetrySourceCommand request,
        CancellationToken cancellationToken
    )
    {
        var source =
            await _sources.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new TelemetrySourceNotFoundException(request.Id);

        _logger.LogInformation(
            "Testing telemetry source {SourceName} ({Kind}).",
            source.Name,
            source.Kind
        );

        var connector = _connectors.Resolve(source.Kind);

        return await connector.TestAsync(source, cancellationToken);
    }
}
