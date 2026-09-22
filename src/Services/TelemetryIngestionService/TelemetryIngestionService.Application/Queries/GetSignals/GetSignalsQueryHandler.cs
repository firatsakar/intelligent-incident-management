using MediatR;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Application.Queries.GetSignals;

public sealed class GetSignalsQueryHandler : IRequestHandler<GetSignalsQuery, SignalPageDto>
{
    /// <summary>
    /// The ceiling on both paths. A screen that wants more asks again with an offset, which is a
    /// decision the operator makes rather than one the customer's error rate makes for them.
    /// </summary>
    public const int MaxLimit = 200;

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

    public async Task<SignalPageDto> Handle(
        GetSignalsQuery request,
        CancellationToken cancellationToken
    )
    {
        var (signals, total) = await FetchAsync(request, cancellationToken);

        if (signals.Count == 0)
            return new SignalPageDto { Items = [], TotalCount = total };

        // One read for the whole page rather than one per row, and only the signatures these
        // signals actually refer to.
        var signatureIds = signals.Select(x => x.ErrorSignatureId).Distinct().ToList();

        var byId = (await _signatures.GetByIdsAsync(signatureIds, cancellationToken)).ToDictionary(
            signature => signature.Id
        );

        return new SignalPageDto
        {
            Items = signals
                .Select(signal => SignalDto.FromDomain(signal, Lookup(byId, signal)))
                .ToList(),
            TotalCount = total,
        };
    }

    private async Task<(IReadOnlyList<Signal> Items, int TotalCount)> FetchAsync(
        GetSignalsQuery request,
        CancellationToken cancellationToken
    )
    {
        var limit = Math.Clamp(request.Limit, 1, MaxLimit);
        var offset = Math.Max(0, request.Offset);

        if (request.From is { } from && request.To is { } to)
            return await _signals.GetRecentAsync(from, to, limit, offset, cancellationToken);

        // The status path has never paged and does not start now: it is a queue, and a queue with
        // an offset is a queue somebody is going to read twice.
        var items = await _signals.GetByStatusAsync(request.Status, limit, cancellationToken);

        return (items, items.Count);
    }

    private static ErrorSignature? Lookup(
        IReadOnlyDictionary<Guid, ErrorSignature> byId,
        Signal signal
    ) => byId.TryGetValue(signal.ErrorSignatureId, out var signature) ? signature : null;
}
