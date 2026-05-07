namespace BuildingBlocks.Contracts;

public sealed record IncidentDetectedEvent(
    Guid IncidentId,
    string ServiceName,
    string ErrorType,
    string Message,
    DateTime OccurredAt,
    string Severity
);
