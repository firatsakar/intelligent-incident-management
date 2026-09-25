using MediatR;
using Microsoft.Extensions.Logging;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.Commands.IngestLogBatch;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Exceptions;

namespace TelemetryIngestionService.Application.Commands.PollTelemetrySource;

// One poll of one source: fetch since the cursor, hand the batch to the ingest pipeline, and move
// the cursor. What receiving an event means is IngestLogBatchCommand's to decide — the pushed
// path uses the same one.
public sealed class PollTelemetrySourceCommandHandler
    : IRequestHandler<PollTelemetrySourceCommand, PollResult>
{
    private const int MaxEventsPerPoll = 200;

    private readonly ITelemetrySourceRepository _sources;
    private readonly ISourceCursorRepository _cursors;
    private readonly ITelemetrySourceConnectorResolver _connectors;
    private readonly ISender _sender;
    private readonly ILogger<PollTelemetrySourceCommandHandler> _logger;

    public PollTelemetrySourceCommandHandler(
        ITelemetrySourceRepository sources,
        ISourceCursorRepository cursors,
        ITelemetrySourceConnectorResolver connectors,
        ISender sender,
        ILogger<PollTelemetrySourceCommandHandler> logger
    )
    {
        _sources = sources;
        _cursors = cursors;
        _connectors = connectors;
        _sender = sender;
        _logger = logger;
    }

    public async Task<PollResult> Handle(
        PollTelemetrySourceCommand request,
        CancellationToken cancellationToken
    )
    {
        var source =
            await _sources.GetByIdAsync(request.SourceId, cancellationToken)
            ?? throw new TelemetrySourceNotFoundException(request.SourceId);

        var cursor = await _cursors.GetOrCreateAsync(source.Id, cancellationToken);

        ConnectorFetchResult fetch;

        try
        {
            var connector = _connectors.Resolve(source.Kind);

            fetch = await connector.FetchAsync(source, cursor, MaxEventsPerPoll, cancellationToken);
        }
        catch (Exception ex)
        {
            // A source being unreachable is an operational fact, not a crash. It is recorded on
            // the cursor so an operator can see which source is failing and why, and the poll
            // loop carries on with the others.
            _logger.LogError(ex, "Polling telemetry source {SourceName} failed.", source.Name);

            cursor.RecordFailure(ex.Message);
            await _cursors.SaveChangesAsync(cancellationToken);

            return PollResult.Failed(ex.Message);
        }

        var ingested = await _sender.Send(
            new IngestLogBatchCommand(source.Id, fetch.Events),
            cancellationToken
        );

        cursor.Advance(fetch.NextPosition, fetch.LastEventTimestamp);
        await _cursors.SaveChangesAsync(cancellationToken);

        return new PollResult
        {
            Fetched = ingested.Received,
            Duplicates = ingested.Duplicates,
            Stored = ingested.Stored,
            Folded = ingested.Folded,
            SignaturesTouched = ingested.SignaturesTouched,
        };
    }
}
