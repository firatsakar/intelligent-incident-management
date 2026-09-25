using MediatR;
using Microsoft.Extensions.Logging;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Enums;
using TelemetryIngestionService.Domain.Exceptions;

namespace TelemetryIngestionService.Application.Commands.TestTelemetrySource;

public sealed class TestTelemetrySourceCommandHandler
    : IRequestHandler<TestTelemetrySourceCommand, ConnectorTestResult>
{
    private readonly ITelemetrySourceRepository _sources;
    private readonly ISourceCursorRepository _cursors;
    private readonly ITelemetrySourceConnectorResolver _connectors;
    private readonly ILogger<TestTelemetrySourceCommandHandler> _logger;

    public TestTelemetrySourceCommandHandler(
        ITelemetrySourceRepository sources,
        ISourceCursorRepository cursors,
        ITelemetrySourceConnectorResolver connectors,
        ILogger<TestTelemetrySourceCommandHandler> logger
    )
    {
        _sources = sources;
        _cursors = cursors;
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

        // Nothing to call for a pushed source — the sender is the customer's, somewhere we cannot
        // reach. What proves it works is that something has arrived.
        if (source.Kind.IsPushed())
            return await LastReceiptAsync(source.Id, cancellationToken);

        var connector = _connectors.Resolve(source.Kind);

        return await connector.TestAsync(source, cancellationToken);
    }

    private async Task<ConnectorTestResult> LastReceiptAsync(
        Guid sourceId,
        CancellationToken cancellationToken
    )
    {
        // Read, never saved: a test must not leave a cursor behind for a source that has not
        // received anything.
        var cursor = await _cursors.GetOrCreateAsync(sourceId, cancellationToken);

        return cursor.LastPolledAt is { } receivedAt
            ? new ConnectorTestResult { IsSuccess = true, LastReceivedAt = receivedAt }
            : ConnectorTestResult.Failure(
                "Nothing has arrived yet. Point a collector at /otlp/v1/logs with this source's key."
            );
    }
}
