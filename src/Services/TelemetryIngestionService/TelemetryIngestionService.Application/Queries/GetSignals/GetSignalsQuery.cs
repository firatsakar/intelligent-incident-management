using MediatR;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Application.Queries.GetSignals;

// Defaults to the weak band, which is the queue worth a human's attention: confident enough to
// record, not confident enough to wake anyone.
public sealed record GetSignalsQuery(SignalStatus Status = SignalStatus.Weak, int Limit = 50)
    : IRequest<IReadOnlyList<SignalDto>>;
