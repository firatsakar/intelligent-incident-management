using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.SharedKernel;
using MediatR;
using Microsoft.Extensions.Logging;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.Commands.DetectSignals;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;
using TelemetryIngestionService.Domain.Services;

namespace TelemetryIngestionService.Application.Commands.IngestLogBatch;

// Drop what we already hold, normalise the rest, fold each signature's burst onto a few samples,
// store them, and fold the errors into their signatures. Moved here from the poll handler so the
// pushed path and the pulled path cannot drift apart: there is one definition of what receiving
// a log line means.
public sealed class IngestLogBatchCommandHandler : IRequestHandler<IngestLogBatchCommand, IngestResult>
{
    // How far ahead of our own clock a source timestamp may sit before we call it skew rather
    // than latency.
    private static readonly TimeSpan ClockSkewTolerance = TimeSpan.FromMinutes(2);

    // The column widths of log_records and error_signatures.
    private const int MaxSourceEventIdLength = 128;
    private const int MaxServiceLength = 128;
    private const int MaxMessageLength = 4096;
    private const int MaxExceptionTypeLength = 512;
    private const int MaxStackTraceLength = 8192;

    private readonly ILogRecordRepository _logRecords;
    private readonly IErrorSignatureRepository _signatures;
    private readonly ISender _sender;
    private readonly IRealtimeNotifier _realtime;
    private readonly ILogger<IngestLogBatchCommandHandler> _logger;
    private readonly IOrganizationContext _organization;

    public IngestLogBatchCommandHandler(
        ILogRecordRepository logRecords,
        IErrorSignatureRepository signatures,
        ISender sender,
        IRealtimeNotifier realtime,
        ILogger<IngestLogBatchCommandHandler> logger,
        IOrganizationContext organization
    )
    {
        _logRecords = logRecords;
        _signatures = signatures;
        _sender = sender;
        _realtime = realtime;
        _logger = logger;
        _organization = organization;
    }

    public async Task<IngestResult> Handle(
        IngestLogBatchCommand request,
        CancellationToken cancellationToken
    )
    {
        if (request.Events.Count == 0)
            return IngestResult.Nothing();

        // Bounded before anything compares ids, so a long id is matched in the form it was stored.
        var bounded = request.Events.Select(Bounded).ToList();

        var fresh = await FilterAlreadyStoredAsync(request.SourceId, bounded, cancellationToken);
        var duplicates = request.Events.Count - fresh.Count;

        if (fresh.Count == 0)
        {
            _logger.LogDebug(
                "Telemetry source {SourceId}: all {Count} event(s) were already stored.",
                request.SourceId,
                request.Events.Count
            );

            return IngestResult.Nothing(request.Events.Count, duplicates);
        }

        var prepared = fresh.Select(Prepare).ToList();

        var weights = SampleFolding.Weigh(
            prepared
                .Select(x => new SampleFolding.Candidate(
                    x.Fingerprint,
                    x.Raw.Timestamp,
                    x.Raw.Severity == LogSeverity.Fatal
                ))
                .ToList()
        );

        var records = new List<LogRecord>(fresh.Count);
        var occurrences = new Dictionary<string, SignatureOccurrence>();

        for (var i = 0; i < prepared.Count; i++)
        {
            var (raw, normalized, fingerprint) = prepared[i];

            if (weights[i] > 0)
                records.Add(
                    LogRecord.Create(
                        _organization.Required,
                        request.SourceId,
                        raw.SourceEventId,
                        raw.Service,
                        raw.Severity,
                        raw.Message,
                        normalized,
                        raw.ExceptionType,
                        raw.StackTrace,
                        fingerprint,
                        raw.Timestamp,
                        ClockSkewTolerance,
                        weights[i]
                    )
                );

            if (fingerprint is null)
                continue;

            // Every event counts towards its signature, stored or folded — the signature's
            // counters were never about rows. Folded here first, so one signature seen 300 times
            // costs one read and one write rather than 300 of each.
            if (occurrences.TryGetValue(fingerprint, out var existing))
                occurrences[fingerprint] = existing.Add(raw.Timestamp);
            else
                occurrences[fingerprint] = SignatureOccurrence.First(raw, normalized);
        }

        await _logRecords.AddRangeAsync(records, cancellationToken);
        await _logRecords.SaveChangesAsync(cancellationToken);

        await ApplyOccurrencesAsync(occurrences, cancellationToken);

        // Storing what happened and deciding what it means are separate jobs, and detection runs
        // only on the signatures this batch actually touched.
        if (occurrences.Count > 0)
            await _sender.Send(new DetectSignalsCommand(occurrences.Keys.ToList()), cancellationToken);

        var folded = fresh.Count - records.Count;

        _logger.LogInformation(
            "Telemetry source {SourceId}: stored {Stored} of {Received} event(s), {Folded} folded onto samples, {Duplicates} already held, {Signatures} signature(s) touched.",
            request.SourceId,
            records.Count,
            request.Events.Count,
            folded,
            duplicates,
            occurrences.Count
        );

        // One message for the batch, carrying counts rather than rows. A screen does not need the
        // rows to know its window is out of date, and a row-level push would be dozens of
        // messages a second fanned to every connected client of the organisation.
        await _realtime.IngestionCompletedAsync(
            new IngestionTickDto
            {
                TelemetrySourceId = request.SourceId,
                NewRecords = records.Count,
                TouchedSignatures = occurrences.Count,
                LatestEventAt = fresh.Max(x => x.Timestamp),
                CompletedAt = DateTime.UtcNow,
            },
            cancellationToken
        );

        return new IngestResult
        {
            Received = request.Events.Count,
            Duplicates = duplicates,
            Stored = records.Count,
            Folded = folded,
            SignaturesTouched = occurrences.Count,
        };
    }

    private static (RawLogEvent Raw, string Normalized, string? Fingerprint) Prepare(RawLogEvent raw)
    {
        var normalized = LogFingerprint.Normalize(raw.Message, raw.MessageTemplate);

        // Only errors get a fingerprint. Information and Warning records are kept as context
        // for the evidence window, but they are not what incidents are raised from.
        var fingerprint = raw.Severity >= LogSeverity.Error
            ? LogFingerprint.Compute(raw.Service, raw.ExceptionType, normalized)
            : null;

        return (raw, normalized, fingerprint);
    }

    // The columns have limits and the sources do not. One stack trace past 8 KB used to fail the
    // insert for the whole batch — and a poll that fails the same way every time never advances
    // its cursor. Cut here, the one place both the pulled and the pushed path pass through.
    private static RawLogEvent Bounded(RawLogEvent raw) =>
        raw with
        {
            SourceEventId = raw.SourceEventId is { Length: > MaxSourceEventIdLength } id
                // Hashed rather than cut, so two long ids that share a prefix stay two events.
                ? Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(id)))
                : raw.SourceEventId,
            Service = Cut(raw.Service, MaxServiceLength),
            Message = Cut(raw.Message, MaxMessageLength),
            ExceptionType = raw.ExceptionType is null ? null : Cut(raw.ExceptionType, MaxExceptionTypeLength),
            StackTrace = raw.StackTrace is null ? null : Cut(raw.StackTrace, MaxStackTraceLength),
        };

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];

    private async Task<IReadOnlyList<RawLogEvent>> FilterAlreadyStoredAsync(
        Guid sourceId,
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
            sourceId,
            ids,
            cancellationToken
        );

        // An event without a source id cannot be deduplicated, so it is kept rather than dropped.
        // The same id twice in one batch is one event sent twice.
        var seen = new HashSet<string>(StringComparer.Ordinal);

        return events
            .Where(x =>
                x.SourceEventId is null
                || (!known.Contains(x.SourceEventId) && seen.Add(x.SourceEventId))
            )
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
                _organization.Required,
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
