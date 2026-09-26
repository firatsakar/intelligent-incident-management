using IncidentService.Application.Abstractions;
using IncidentService.Application.Commands.CreateIncident;
using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IncidentService.Application.Commands.IntakeIncident;

public sealed class IntakeIncidentCommandHandler : IRequestHandler<IntakeIncidentCommand, IntakeIncidentResult>
{
    private readonly IIncidentRepository _incidents;
    private readonly ISender _sender;
    private readonly ILogger<IntakeIncidentCommandHandler> _logger;

    public IntakeIncidentCommandHandler(
        IIncidentRepository incidents,
        ISender sender,
        ILogger<IntakeIncidentCommandHandler> logger
    )
    {
        _incidents = incidents;
        _sender = sender;
        _logger = logger;
    }

    public async Task<IntakeIncidentResult> Handle(IntakeIncidentCommand request, CancellationToken cancellationToken)
    {
        var externalId = string.IsNullOrWhiteSpace(request.ExternalId) ? null : request.ExternalId.Trim();

        // Alerting tools resend what is still firing; while its incident is open, the same name is
        // the same problem. Once it is resolved, the next one is a recurrence and opens anew.
        if (externalId is not null && await _incidents.GetOpenByExternalIdAsync(externalId, cancellationToken) is { } open)
            return Matched(open, externalId, request.KeyName);

        try
        {
            // The same command the console's incidents go through, so an incident that arrives
            // this way is announced, analysed and broadcast exactly like any other.
            var created = await _sender.Send(
                new CreateIncidentCommand
                {
                    Title = request.Title.Trim(),
                    Description = request.Description.Trim(),
                    Priority = request.Priority ?? IncidentPriority.Medium,
                    Source = IncidentSource.Alert,
                    DetectedAt = request.DetectedAt,
                    ExternalId = externalId,
                    ReportedBy = request.KeyName,
                },
                cancellationToken
            );

            _logger.LogInformation(
                "Incident {IncidentId} opened through the incident API with key {KeyName} (external id: {ExternalId}).",
                created.Id,
                request.KeyName,
                externalId ?? "none"
            );

            return new IntakeIncidentResult(created.Id, Created: true);
        }
        catch (DuplicateExternalIdException) when (externalId is not null)
        {
            // Another request with the same external id got its incident saved between the look
            // above and this save. Its incident is the answer to both.
            var winner =
                await _incidents.GetOpenByExternalIdAsync(externalId, cancellationToken)
                ?? throw new InvalidOperationException($"No open incident for external id '{externalId}' after a duplicate.");

            return Matched(winner, externalId, request.KeyName);
        }
    }

    private IntakeIncidentResult Matched(Incident incident, string externalId, string keyName)
    {
        _logger.LogInformation(
            "Incident API request with key {KeyName} matched open incident {IncidentId} by external id {ExternalId}; nothing opened.",
            keyName,
            incident.Id,
            externalId
        );

        return new IntakeIncidentResult(incident.Id, Created: false);
    }
}
