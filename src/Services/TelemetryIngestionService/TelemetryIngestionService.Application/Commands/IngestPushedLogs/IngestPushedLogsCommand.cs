using MediatR;
using TelemetryIngestionService.Application.DTOs;

namespace TelemetryIngestionService.Application.Commands.IngestPushedLogs;

// A batch a sender pushed to us, already decoded. Sent from inside the organisation scope the
// endpoint took from the key's source row — this command never decides whose the batch is.
public sealed record IngestPushedLogsCommand(Guid SourceId, IReadOnlyList<RawLogEvent> Events)
    : IRequest<PushResult>;

public sealed record PushResult
{
    public required int Received { get; init; }

    // Below the source's minimum severity. Dropped deliberately, so not "rejected" in OTLP's sense.
    public required int BelowMinimum { get; init; }

    public required IngestResult Ingested { get; init; }
}
