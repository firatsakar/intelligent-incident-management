using MediatR;
using TelemetryIngestionService.Application.DTOs;

namespace TelemetryIngestionService.Application.Queries.GetEvidence;

public sealed record GetEvidenceQuery(string? Service, DateTime From, DateTime To)
    : IRequest<EvidenceWindowDto>;
