using MediatR;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Application.Queries.GetTelemetryStats;

public sealed class GetTelemetryStatsQueryHandler
    : IRequestHandler<GetTelemetryStatsQuery, TelemetryStatsDto>
{
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromHours(24);

    /// <summary>
    /// Wider than this stops being a dashboard question. The log side is counted in the database
    /// so it does not care, but the signal side comes back as rows, and that is the side a
    /// ceiling protects.
    /// </summary>
    public static readonly TimeSpan MaxWindow = TimeSpan.FromDays(30);

    private readonly ILogRecordRepository _logRecords;
    private readonly ISignalRepository _signals;
    private readonly IErrorSignatureRepository _signatures;

    public GetTelemetryStatsQueryHandler(
        ILogRecordRepository logRecords,
        ISignalRepository signals,
        IErrorSignatureRepository signatures
    )
    {
        _logRecords = logRecords;
        _signals = signals;
        _signatures = signatures;
    }

    public async Task<TelemetryStatsDto> Handle(
        GetTelemetryStatsQuery request,
        CancellationToken cancellationToken
    )
    {
        var to = request.To ?? DateTime.UtcNow;
        var from = request.From ?? to - DefaultWindow;

        if (to - from > MaxWindow)
            from = to - MaxWindow;

        var logs = await _logRecords.GetWindowSummaryAsync(from, to, cancellationToken);
        var signalRows = await _signals.GetWindowStatsRowsAsync(from, to, cancellationToken);

        var signatureIds = signalRows.Select(row => row.ErrorSignatureId).Distinct().ToList();

        var signaturesById = (
            await _signatures.GetByIdsAsync(signatureIds, cancellationToken)
        ).ToDictionary(signature => signature.Id);

        return new TelemetryStatsDto
        {
            From = from,
            To = to,
            Funnel = BuildFunnel(logs, signalRows),
            Services = BuildServices(logs, signalRows, signaturesById),
        };
    }

    private static FunnelDto BuildFunnel(
        LogWindowSummary logs,
        IReadOnlyList<SignalStatsRow> signalRows
    )
    {
        var byStatus = signalRows
            .GroupBy(row => row.Status)
            .ToDictionary(group => group.Key.ToString(), group => group.Count());

        foreach (var name in Enum.GetNames<SignalStatus>())
            byStatus.TryAdd(name, 0);

        return new FunnelDto
        {
            LogRecords = logs.TotalRecords,
            Signatures = logs.DistinctFingerprints,
            Signals = signalRows.Count,
            SignalsByStatus = byStatus,
            // Deduplicated is deliberately NOT counted as "not raised". A deduplicated signal was
            // folded into an incident that is already open, so somebody was woken — just earlier.
            // Counting it here would let the product claim restraint it did not show.
            NotRaised = signalRows.Count(row =>
                row.Status is SignalStatus.Weak or SignalStatus.Recorded or SignalStatus.Suppressed
            ),
            LogRecordsBySeverity = logs.BySeverity.ToDictionary(
                pair => pair.Key.ToString(),
                pair => pair.Value
            ),
        };
    }

    private static List<ServiceHealthDto> BuildServices(
        LogWindowSummary logs,
        IReadOnlyList<SignalStatsRow> signalRows,
        IReadOnlyDictionary<Guid, ErrorSignature> signaturesById
    )
    {
        // Signals carry a signature id, not a service; the service lives on the signature. Rows
        // whose signature has since gone are grouped under a name rather than dropped, because a
        // silently shorter table is the kind of wrong that never gets noticed.
        var rowsByService = signalRows
            .GroupBy(row => ServiceOf(signaturesById, row))
            .ToDictionary(group => group.Key, group => group.ToList());

        // The union, not the signal side alone: a service can produce errors all week without any
        // of them crossing a detection rule, and that is a fact about the service worth a row.
        var services = rowsByService.Keys.Union(logs.ByService.Keys).OrderBy(name => name);

        var result = new List<ServiceHealthDto>();

        foreach (var service in services)
        {
            rowsByService.TryGetValue(service, out var rows);
            rows ??= [];

            var top = rows.GroupBy(row => row.ErrorSignatureId)
                .Select(group => new
                {
                    group.Key,
                    Occurrences = group.Sum(row => row.OccurrenceCount),
                })
                .OrderByDescending(x => x.Occurrences)
                .FirstOrDefault();

            result.Add(
                new ServiceHealthDto
                {
                    Service = service,
                    LogRecords = logs.ByService.GetValueOrDefault(service),
                    Signals = rows.Count,
                    Promoted = rows.Count(row => row.Status == SignalStatus.Promoted),
                    Incidents = rows.Where(row => row.IncidentId.HasValue)
                        .Select(row => row.IncidentId!.Value)
                        .Distinct()
                        .Count(),
                    TopSignature = top is null ? null : LabelOf(signaturesById, top.Key),
                    TopSignatureOccurrences = top?.Occurrences ?? 0,
                    LastSignalAt = rows.Count == 0
                        ? null
                        : rows.Max(row => row.DetectedAt),
                }
            );
        }

        return result;
    }

    private const string UnknownService = "(signature gone)";

    private static string ServiceOf(
        IReadOnlyDictionary<Guid, ErrorSignature> signaturesById,
        SignalStatsRow row
    ) =>
        signaturesById.TryGetValue(row.ErrorSignatureId, out var signature)
            ? signature.Service
            : UnknownService;

    /// <summary>
    /// The exception type where there is one, the normalised message otherwise — the same choice
    /// the heat map makes, so a service row and a map tile name the same failure the same way.
    /// </summary>
    private static string? LabelOf(
        IReadOnlyDictionary<Guid, ErrorSignature> signaturesById,
        Guid signatureId
    )
    {
        if (!signaturesById.TryGetValue(signatureId, out var signature))
            return null;

        return signature.ExceptionType ?? signature.NormalizedMessage;
    }
}
