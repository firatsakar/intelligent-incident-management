using MediatR;
using TelemetryIngestionService.Application.DTOs;

namespace TelemetryIngestionService.Application.Commands.IngestLogBatch;

// One batch of events from one source, however they arrived — pulled by the poller or pushed to
// the OTLP endpoint. Everything after arrival is the same pipeline: drop what is already held,
// normalise, fold, store, update signatures, detect.
public sealed record IngestLogBatchCommand(Guid SourceId, IReadOnlyList<RawLogEvent> Events)
    : IRequest<IngestResult>;
