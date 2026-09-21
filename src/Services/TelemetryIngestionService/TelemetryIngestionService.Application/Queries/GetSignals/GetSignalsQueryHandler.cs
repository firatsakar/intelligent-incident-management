using MediatR;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;

namespace TelemetryIngestionService.Application.Queries.GetSignals;

public sealed class GetSignalsQueryHandler
    : IRequestHandler<GetSignalsQuery, IReadOnlyList<SignalDto>>
{
    private readonly ISignalRepository _signals;

    public GetSignalsQueryHandler(ISignalRepository signals)
    {
        _signals = signals;
    }

    public async Task<IReadOnlyList<SignalDto>> Handle(
        GetSignalsQuery request,
        CancellationToken cancellationToken
    )
    {
        var signals = await _signals.GetByStatusAsync(
            request.Status,
            Math.Clamp(request.Limit, 1, 200),
            cancellationToken
        );

        return signals.Select(SignalDto.FromDomain).ToList();
    }
}
