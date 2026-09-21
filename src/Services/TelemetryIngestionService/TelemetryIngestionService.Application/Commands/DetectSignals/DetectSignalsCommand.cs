using MediatR;

namespace TelemetryIngestionService.Application.Commands.DetectSignals;

// Evaluates the signatures a poll just touched. Runs after ingestion, never during it: storing
// what happened and deciding what it means are separate jobs.
public sealed record DetectSignalsCommand(IReadOnlyCollection<string> Fingerprints)
    : IRequest<int>;
