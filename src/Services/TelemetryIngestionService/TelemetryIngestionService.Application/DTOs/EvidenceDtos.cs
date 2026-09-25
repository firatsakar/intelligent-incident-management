using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;

namespace TelemetryIngestionService.Application.DTOs;

// What happened in a window, for a human or the Adım 19 frontend to look at. Deliberately not an
// AI tool: shipping raw telemetry to a model is the expensive thing this design avoids — the AI
// gets the compact evidence summary inside the incident description instead.
public sealed record EvidenceWindowDto
{
    public required DateTime From { get; init; }
    public required DateTime To { get; init; }
    public string? Service { get; init; }
    public required int TotalLogRecords { get; init; }

    // Signals and signatures used to come back whole while only the log records were capped, so
    // a busy window could return three unbounded collections out of three. Every list here now
    // reports the count it was cut from.
    public required int TotalSignals { get; init; }
    public required int TotalSignatures { get; init; }
    public required IReadOnlyList<LogRecordDto> LogRecords { get; init; }
    public required IReadOnlyList<ErrorSignatureDto> Signatures { get; init; }
    public required IReadOnlyList<SignalDto> Signals { get; init; }
}

// A window of signals with the count it was cut from, so a truncated list can say so. Kept local
// rather than reaching for IncidentService's PagedResult: two service domains never reference
// each other, and a shared paging contract is a BuildingBlocks decision for the day a third
// caller wants one.
public sealed record SignalPageDto
{
    public required IReadOnlyList<SignalDto> Items { get; init; }
    public required int TotalCount { get; init; }
}

public sealed record LogRecordDto
{
    public required Guid Id { get; init; }
    public required string Service { get; init; }
    public required LogSeverity Severity { get; init; }
    public required string Message { get; init; }
    public string? ExceptionType { get; init; }
    public string? Fingerprint { get; init; }

    // How many events this row stands for — above one when a burst was folded onto it.
    public required int Occurrences { get; init; }
    public required DateTime Timestamp { get; init; }
    public required DateTime IngestedAt { get; init; }
    public required bool HasClockSkew { get; init; }

    public static LogRecordDto FromDomain(LogRecord record) =>
        new()
        {
            Id = record.Id,
            Service = record.Service,
            Severity = record.Severity,
            Message = record.Message,
            ExceptionType = record.ExceptionType,
            Fingerprint = record.Fingerprint,
            Occurrences = record.Occurrences,
            Timestamp = record.Timestamp,
            IngestedAt = record.IngestedAt,
            HasClockSkew = record.HasClockSkew,
        };
}

public sealed record ErrorSignatureDto
{
    public required Guid Id { get; init; }
    public required string Fingerprint { get; init; }
    public required string Service { get; init; }
    public string? ExceptionType { get; init; }
    public required string NormalizedMessage { get; init; }
    public required DateTime FirstSeenAt { get; init; }
    public required DateTime LastSeenAt { get; init; }
    public required long OccurrenceCount { get; init; }
    public Guid? CurrentIncidentId { get; init; }
    public required bool IsMuted { get; init; }
    public required int PromotionCount { get; init; }
    public required int ConfirmedRealCount { get; init; }
    public required int FalsePositiveCount { get; init; }

    public static ErrorSignatureDto FromDomain(ErrorSignature signature) =>
        new()
        {
            Id = signature.Id,
            Fingerprint = signature.Fingerprint,
            Service = signature.Service,
            ExceptionType = signature.ExceptionType,
            NormalizedMessage = signature.NormalizedMessage,
            FirstSeenAt = signature.FirstSeenAt,
            LastSeenAt = signature.LastSeenAt,
            OccurrenceCount = signature.OccurrenceCount,
            CurrentIncidentId = signature.CurrentIncidentId,
            IsMuted = signature.IsMuted,
            PromotionCount = signature.PromotionCount,
            ConfirmedRealCount = signature.ConfirmedRealCount,
            FalsePositiveCount = signature.FalsePositiveCount,
        };
}

public sealed record SignalDto
{
    public required Guid Id { get; init; }
    public required Guid ErrorSignatureId { get; init; }
    public required SignalKind Kind { get; init; }
    public required SignalStatus Status { get; init; }
    public required DateTime DetectedAt { get; init; }
    public required DateTime WindowStart { get; init; }
    public required DateTime WindowEnd { get; init; }
    public required long OccurrenceCount { get; init; }
    public required double Confidence { get; init; }

    // Carried from the signature rather than looked up per row. A signal that cannot say which
    // service broke and how is close to useless to a human, and the heat map cannot place it at
    // all — the signal itself only knows a signature id.
    public string? Service { get; init; }
    public string? ExceptionType { get; init; }
    public string? NormalizedMessage { get; init; }

    // Why the gate decided what it decided.
    public required IReadOnlyDictionary<string, double> ScoreBreakdown { get; init; }

    public string? Reason { get; init; }
    public Guid? IncidentId { get; init; }

    // The signature is optional: a signal outliving its signature should still be listed, just
    // without the description.
    public static SignalDto FromDomain(Signal signal, ErrorSignature? signature = null) =>
        new()
        {
            Id = signal.Id,
            Service = signature?.Service,
            ExceptionType = signature?.ExceptionType,
            NormalizedMessage = signature?.NormalizedMessage,
            ErrorSignatureId = signal.ErrorSignatureId,
            Kind = signal.Kind,
            Status = signal.Status,
            DetectedAt = signal.DetectedAt,
            WindowStart = signal.WindowStart,
            WindowEnd = signal.WindowEnd,
            OccurrenceCount = signal.OccurrenceCount,
            Confidence = signal.Confidence,
            ScoreBreakdown = signal.ScoreBreakdown,
            Reason = signal.Reason,
            IncidentId = signal.IncidentId,
        };
}
