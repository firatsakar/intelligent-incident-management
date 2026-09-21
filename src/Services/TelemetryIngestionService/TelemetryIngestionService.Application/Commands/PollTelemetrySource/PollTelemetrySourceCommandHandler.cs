using MediatR;
using Microsoft.Extensions.Logging;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;
using TelemetryIngestionService.Domain.Exceptions;
using TelemetryIngestionService.Domain.Services;

namespace TelemetryIngestionService.Application.Commands.PollTelemetrySource;

// One poll of one source: fetch since the cursor, drop what we already hold, normalise the rest
// into log records, and fold the errors into their signatures.
public sealed class PollTelemetrySourceCommandHandler
    : IRequestHandler<PollTelemetrySourceCommand, PollResult>
{
    private const int MaxEventsPerPoll = 200;

    // How far ahead of our own clock a source timestamp may sit before we call it skew rather
    // than latency.
    private static readonly TimeSpan ClockSkewTolerance = TimeSpan.FromMinutes(2);

    private readonly ITelemetrySourceRepository _sources;
    private readonly ISourceCursorRepository _cursors;
    private readonly ILogRecordRepository _logRecords;
    private readonly IErrorSignatureRepository _signatures;
    private readonly ITelemetrySourceConnectorResolver _connectors;
    private readonly ILogger<PollTelemetrySourceCommandHandler> _logger;

    public PollTelemetrySourceCommandHandler(
        ITelemetrySourceRepository sources,
        ISourceCursorRepository cursors,
        ILogRecordRepository logRecords,
        IErrorSignatureRepository signatures,
        ITelemetrySourceConnectorResolver connectors,
        ILogger<PollTelemetrySourceCommandHandler> logger
    )
    {
        _sources = sources;
        _cursors = cursors;
        _logRecords = logRecords;
        _signatures = signatures;
        _connectors = connectors;
        _logger = logger;
    }

    public async Task<PollResult> Handle(
        PollTelemetrySourceCommand request,
        CancellationToken cancellationToken
    )
    {
        var source =
            await _sources.GetByIdAsync(request.SourceId, cancellationToken)
            ?? throw new TelemetrySourceNotFoundException(request.SourceId);

        var cursor = await _cursors.GetOrCreateAsync(source.Id, cancellationToken);

        ConnectorFetchResult fetch;

        try
        {
            var connector = _connectors.Resolve(source.Kind);

            fetch = await connector.FetchAsync(source, cursor, MaxEventsPerPoll, cancellationToken);
        }
        catch (Exception ex)
        {
            // A source being unreachable is an operational fact, not a crash. It is recorded on
            // the cursor so an operator can see which source is failing and why, and the poll
            // loop carries on with the others.
            _logger.LogError(ex, "Polling telemetry source {SourceName} failed.", source.Name);

            cursor.RecordFailure(ex.Message);
            await _cursors.SaveChangesAsync(cancellationToken);

            return PollResult.Failed(ex.Message);
        }

        var stored = await IngestAsync(source, fetch, cancellationToken);

        cursor.Advance(fetch.NextPosition, fetch.LastEventTimestamp);
        await _cursors.SaveChangesAsync(cancellationToken);

        return stored;
    }

    private async Task<PollResult> IngestAsync(
        TelemetrySource source,
        ConnectorFetchResult fetch,
        CancellationToken cancellationToken
    )
    {
        if (fetch.Events.Count == 0)
            return new PollResult
            {
                Fetched = 0,
                Duplicates = 0,
                Stored = 0,
                SignaturesTouched = 0,
            };

        var fresh = await FilterAlreadyStoredAsync(source, fetch.Events, cancellationToken);
        var duplicates = fetch.Events.Count - fresh.Count;

        if (fresh.Count == 0)
        {
            _logger.LogDebug(
                "Telemetry source {SourceName}: all {Count} fetched event(s) were already stored.",
                source.Name,
                fetch.Events.Count
            );

            return new PollResult
            {
                Fetched = fetch.Events.Count,
                Duplicates = duplicates,
                Stored = 0,
                SignaturesTouched = 0,
            };
        }

        var records = new List<LogRecord>(fresh.Count);
        var occurrences = new Dictionary<string, SignatureOccurrence>();

        foreach (var raw in fresh)
        {
            var normalized = LogFingerprint.Normalize(raw.Message, raw.MessageTemplate);

            // Only errors get a fingerprint. Information and Warning records are kept as context
            // for the evidence window, but they are not what incidents are raised from.
            var fingerprint = raw.Severity >= LogSeverity.Error
                ? LogFingerprint.Compute(raw.Service, raw.ExceptionType, normalized)
                : null;

            records.Add(
                LogRecord.Create(
                    source.Id,
                    raw.SourceEventId,
                    raw.Service,
                    raw.Severity,
                    raw.Message,
                    normalized,
                    raw.ExceptionType,
                    raw.StackTrace,
                    fingerprint,
                    raw.Timestamp,
                    ClockSkewTolerance
                )
            );

            if (fingerprint is null)
                continue;

            // Fold the batch first, so one signature seen 200 times costs one read and one write
            // rather than 200 of each.
            if (occurrences.TryGetValue(fingerprint, out var existing))
                occurrences[fingerprint] = existing.Add(raw.Timestamp);
            else
                occurrences[fingerprint] = SignatureOccurrence.First(raw, normalized);
        }

        await _logRecords.AddRangeAsync(records, cancellationToken);
        await _logRecords.SaveChangesAsync(cancellationToken);

        await ApplyOccurrencesAsync(occurrences, cancellationToken);

        _logger.LogInformation(
            "Telemetry source {SourceName}: stored {Stored} of {Fetched} event(s), {Duplicates} already held, {Signatures} signature(s) touched.",
            source.Name,
            records.Count,
            fetch.Events.Count,
            duplicates,
            occurrences.Count
        );

        return new PollResult
        {
            Fetched = fetch.Events.Count,
            Duplicates = duplicates,
            Stored = records.Count,
            SignaturesTouched = occurrences.Count,
        };
    }

    private async Task<IReadOnlyList<RawLogEvent>> FilterAlreadyStoredAsync(
        TelemetrySource source,
        IReadOnlyList<RawLogEvent> events,
        CancellationToken cancellationToken
    )
    {
        var ids = events
            .Select(x => x.SourceEventId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return events;

        var known = await _logRecords.GetExistingSourceEventIdsAsync(
            source.Id,
            ids,
            cancellationToken
        );

        // An event without a source id cannot be deduplicated, so it is kept rather than dropped.
        return events
            .Where(x => x.SourceEventId is null || !known.Contains(x.SourceEventId))
            .ToList();
    }

    private async Task ApplyOccurrencesAsync(
        Dictionary<string, SignatureOccurrence> occurrences,
        CancellationToken cancellationToken
    )
    {
        if (occurrences.Count == 0)
            return;

        var existing = await _signatures.GetByFingerprintsAsync(
            occurrences.Keys.ToList(),
            cancellationToken
        );

        var byFingerprint = existing.ToDictionary(x => x.Fingerprint);

        foreach (var (fingerprint, occurrence) in occurrences)
        {
            if (byFingerprint.TryGetValue(fingerprint, out var signature))
            {
                signature.RecordOccurrence(occurrence.LastSeenAt, occurrence.Count);

                // The earliest occurrence matters too: a batch can arrive out of order, and
                // DetectedAt is meant to be when the problem started.
                signature.RecordOccurrence(occurrence.FirstSeenAt, count: 0);

                continue;
            }

            var created = ErrorSignature.Create(
                fingerprint,
                occurrence.Service,
                occurrence.ExceptionType,
                occurrence.NormalizedMessage,
                occurrence.FirstSeenAt
            );

            // Create() already counts the first one.
            if (occurrence.Count > 1)
                created.RecordOccurrence(occurrence.LastSeenAt, occurrence.Count - 1);

            await _signatures.AddAsync(created, cancellationToken);
        }

        await _signatures.SaveChangesAsync(cancellationToken);
    }

    private readonly record struct SignatureOccurrence(
        string Service,
        string? ExceptionType,
        string NormalizedMessage,
        DateTime FirstSeenAt,
        DateTime LastSeenAt,
        long Count
    )
    {
        public static SignatureOccurrence First(RawLogEvent raw, string normalizedMessage) =>
            new(raw.Service, raw.ExceptionType, normalizedMessage, raw.Timestamp, raw.Timestamp, 1);

        public SignatureOccurrence Add(DateTime timestamp) =>
            this with
            {
                FirstSeenAt = timestamp < FirstSeenAt ? timestamp : FirstSeenAt,
                LastSeenAt = timestamp > LastSeenAt ? timestamp : LastSeenAt,
                Count = Count + 1,
            };
    }
}
