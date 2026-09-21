using MediatR;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Application.Queries.GetSignals;

public sealed class GetSignalsQueryHandler
    : IRequestHandler<GetSignalsQuery, IReadOnlyList<SignalDto>>
{
    private readonly ISignalRepository _signals;
    private readonly IErrorSignatureRepository _signatures;

    public GetSignalsQueryHandler(
        ISignalRepository signals,
        IErrorSignatureRepository signatures
    )
    {
        _signals = signals;
        _signatures = signatures;
    }

    public async Task<IReadOnlyList<SignalDto>> Handle(
        GetSignalsQuery request,
        CancellationToken cancellationToken
    )
    {
        var signals = await FetchAsync(request, cancellationToken);

        if (signals.Count == 0)
            return [];

        // One read for the whole page rather than one per row, and only the signatures these
        // signals actually refer to.
        var signatureIds = signals.Select(x => x.ErrorSignatureId).Distinct().ToList();

        var byId = (await _signatures.GetByIdsAsync(signatureIds, cancellationToken)).ToDictionary(
            signature => signature.Id
        );

        return signals
            .Select(signal => SignalDto.FromDomain(signal, Lookup(byId, signal)))
            .ToList();
    }

    private Task<IReadOnlyList<Signal>> FetchAsync(
        GetSignalsQuery request,
        CancellationToken cancellationToken
    )
    {
        if (request.From is { } from && request.To is { } to)
            return _signals.GetRecentAsync(from, to, cancellationToken);

        return _signals.GetByStatusAsync(
            request.Status,
            Math.Clamp(request.Limit, 1, 200),
            cancellationToken
        );
    }

    private static ErrorSignature? Lookup(
        IReadOnlyDictionary<Guid, ErrorSignature> byId,
        Signal signal
    ) => byId.TryGetValue(signal.ErrorSignatureId, out var signature) ? signature : null;
}
