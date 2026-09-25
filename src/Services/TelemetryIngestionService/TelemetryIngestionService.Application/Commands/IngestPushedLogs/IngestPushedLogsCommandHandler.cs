using MediatR;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.Commands.IngestLogBatch;
using TelemetryIngestionService.Application.Validators;
using TelemetryIngestionService.Domain.Exceptions;

namespace TelemetryIngestionService.Application.Commands.IngestPushedLogs;

// What is particular to a push, around the pipeline every batch goes through: the source's
// severity floor before, and the record of the receipt after.
public sealed class IngestPushedLogsCommandHandler : IRequestHandler<IngestPushedLogsCommand, PushResult>
{
    private readonly ITelemetrySourceRepository _sources;
    private readonly ISourceCursorRepository _cursors;
    private readonly ISender _sender;

    public IngestPushedLogsCommandHandler(
        ITelemetrySourceRepository sources,
        ISourceCursorRepository cursors,
        ISender sender
    )
    {
        _sources = sources;
        _cursors = cursors;
        _sender = sender;
    }

    public async Task<PushResult> Handle(IngestPushedLogsCommand request, CancellationToken cancellationToken)
    {
        // Read again through the organisation's filter, which the endpoint set from this same row.
        // Finding nothing here would mean the scope and the key disagree.
        var source =
            await _sources.GetByIdAsync(request.SourceId, cancellationToken)
            ?? throw new TelemetrySourceNotFoundException(request.SourceId);

        // The collector should have filtered already; this is for when it did not. Without it one
        // unfiltered collector turns log_records into a copy of the customer's whole log stream.
        var minimum = TelemetrySourceConfigRules.MinimumSeverity(source.Config);
        var kept = request.Events.Where(x => x.Severity >= minimum).ToList();

        var ingested = await _sender.Send(new IngestLogBatchCommand(source.Id, kept), cancellationToken);

        // A pushed source's cursor has no position to keep; it records that something arrived and
        // when, which is what its test and its settings row report. Every accepted batch counts,
        // even one that was all below the floor — the sender is working either way.
        var cursor = await _cursors.GetOrCreateAsync(source.Id, cancellationToken);
        cursor.Advance(position: null, kept.Count > 0 ? kept.Max(x => x.Timestamp) : null);
        await _cursors.SaveChangesAsync(cancellationToken);

        return new PushResult
        {
            Received = request.Events.Count,
            BelowMinimum = request.Events.Count - kept.Count,
            Ingested = ingested,
        };
    }
}
