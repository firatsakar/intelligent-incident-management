using MediatR;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;

namespace TelemetryIngestionService.Application.Queries.GetEvidence;

public sealed class GetEvidenceQueryHandler : IRequestHandler<GetEvidenceQuery, EvidenceWindowDto>
{
    // A window can hold a great many records; the endpoint shows the most recent and reports the
    // true total so a truncated view is visibly truncated.
    private const int MaxLogRecords = 200;

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

        var signals = await _signals.GetRecentAsync(request.From, request.To, cancellationToken);

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
