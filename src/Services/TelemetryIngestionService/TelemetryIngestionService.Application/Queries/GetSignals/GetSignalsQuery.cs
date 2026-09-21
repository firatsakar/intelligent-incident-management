using MediatR;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Application.Queries.GetSignals;

// Two shapes in one query, because they answer the same question at different zoom levels.
//
// Without a window: the queue for one status, most recent first. Defaults to the weak band, which
// is the one worth a human's attention — confident enough to record, not confident enough to wake
// anyone.
//
// With a window: everything detected in that span, across *all* statuses. That is what an
// aggregate view needs; filtering by status first would hide the contrast between the bursts that
// were promoted and the ones that were not, which is the whole point of looking.
public sealed record GetSignalsQuery(
    SignalStatus Status = SignalStatus.Weak,
    int Limit = 50,
    DateTime? From = null,
    DateTime? To = null
) : IRequest<IReadOnlyList<SignalDto>>;
