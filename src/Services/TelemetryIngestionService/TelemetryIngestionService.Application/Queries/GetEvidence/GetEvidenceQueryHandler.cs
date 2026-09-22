using MediatR;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;

namespace TelemetryIngestionService.Application.Queries.GetEvidence;

public sealed class GetEvidenceQueryHandler : IRequestHandler<GetEvidenceQuery, EvidenceWindowDto>
{
    // A window can hold a great many records; the endpoint shows the most recent and reports the
    // true total so a truncated view is visibly truncated.
    private const int MaxLogRecords = 200;

    // Signals used to come back whole here while the log records were capped, which meant the one
    // collection with an explicit limit was the only one that had ever needed one less. The
    // signature list follows from the signals, so capping the signals caps it too.
    private const int MaxSignals = 200;

    private readonly ILogRecordRepository _logRecords;
    private readonly ISignalRepository _signals;
    private readonly IErrorSignatureRepository _signatures;

    public GetEvidenceQueryHandler(
        ILogRecordRepository logRecords,
        ISignalRepository signals,
        IErrorSignatureRepository signatures
    )
    {
        _logRecords = logRecords;
        _signals = signals;
        _signatures = signatures;
    }

    public async Task<EvidenceWindowDto> Handle(
        GetEvidenceQuery request,
        CancellationToken cancellationToken
    )
    {
        var (records, total) = await _logRecords.GetWindowAsync(
            request.Service,
            request.From,
            request.To,
            MaxLogRecords,
            cancellationToken
        );

        var (signals, totalSignals) = await _signals.GetRecentAsync(
            request.From,
            request.To,
            MaxSignals,
            offset: 0,
            cancellationToken
        );

        // Only the signatures these signals and records actually refer to, rather than the whole
        // table.
        var signatureIds = signals.Select(x => x.ErrorSignatureId).Distinct().ToList();

        var signatures = await _signatures.GetByIdsAsync(signatureIds, cancellationToken);

        var byId = signatures.ToDictionary(signature => signature.Id);

        return new EvidenceWindowDto
        {
            From = request.From,
            To = request.To,
            Service = request.Service,
            TotalLogRecords = total,
            TotalSignals = totalSignals,
            // The signature list is derived from the signals shown, so its total is the number of
            // distinct signatures those signals refer to — not a separate query against the whole
            // window, which would report a number this response cannot back up.
            TotalSignatures = signatureIds.Count,
            LogRecords = records.Select(LogRecordDto.FromDomain).ToList(),
            Signatures = signatures.Select(ErrorSignatureDto.FromDomain).ToList(),
            Signals = signals
                .Select(signal =>
                    SignalDto.FromDomain(
                        signal,
                        byId.GetValueOrDefault(signal.ErrorSignatureId)
                    )
                )
                .ToList(),
        };
    }
}
