using BuildingBlocks.SharedKernel;
using MediatR;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Application.Commands.CreateTelemetrySource;

public sealed class CreateTelemetrySourceCommandHandler
    : IRequestHandler<CreateTelemetrySourceCommand, TelemetrySourceDto>
{
    private readonly ITelemetrySourceRepository _sources;
    private readonly IRealtimeNotifier _realtime;
    private readonly IOrganizationContext _organization;

    public CreateTelemetrySourceCommandHandler(
        ITelemetrySourceRepository sources,
        IRealtimeNotifier realtime,
        IOrganizationContext organization
    )
    {
        _sources = sources;
        _realtime = realtime;
        _organization = organization;
    }

    public async Task<TelemetrySourceDto> Handle(
        CreateTelemetrySourceCommand request,
        CancellationToken cancellationToken
    )
    {
        var source = TelemetrySource.Create(
            // The only place a source's owner is decided, and it is decided from the claim of
            // whoever configured it. Everything the detector later does with this source's logs
            // inherits the organisation from here.
            _organization.Required,
            request.Name,
            request.Kind,
            request.Config,
            request.PollIntervalSeconds,
            request.IsEnabled
        );

        await _sources.AddAsync(source, cancellationToken);
        await _sources.SaveChangesAsync(cancellationToken);

        // After the save, never before: what is broadcast has to be what is stored. The DTO
        // masks the credentials, so this carries exactly what a GET would.
        var dto = TelemetrySourceDto.FromDomain(source);
        await _realtime.SourceChangedAsync(dto, cancellationToken);

        return dto;
    }
}
