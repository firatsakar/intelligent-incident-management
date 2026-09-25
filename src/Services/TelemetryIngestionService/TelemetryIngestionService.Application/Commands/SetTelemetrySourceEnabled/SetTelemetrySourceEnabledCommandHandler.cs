using MediatR;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Exceptions;

namespace TelemetryIngestionService.Application.Commands.SetTelemetrySourceEnabled;

public sealed class SetTelemetrySourceEnabledCommandHandler
    : IRequestHandler<SetTelemetrySourceEnabledCommand, TelemetrySourceDto>
{
    private readonly ITelemetrySourceRepository _sources;
    private readonly IRealtimeNotifier _realtime;

    public SetTelemetrySourceEnabledCommandHandler(
        ITelemetrySourceRepository sources,
        IRealtimeNotifier realtime
    )
    {
        _sources = sources;
        _realtime = realtime;
    }

    public async Task<TelemetrySourceDto> Handle(
        SetTelemetrySourceEnabledCommand request,
        CancellationToken cancellationToken
    )
    {
        var source =
            await _sources.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new TelemetrySourceNotFoundException(request.Id);

        if (request.IsEnabled)
            source.Enable();
        else
            source.Disable();

        _sources.Update(source);
        await _sources.SaveChangesAsync(cancellationToken);

        // After the save, never before: what is broadcast has to be what is stored. The DTO
        // masks the credentials, so this carries exactly what a GET would.
        var dto = TelemetrySourceDto.FromDomain(source);
        await _realtime.SourceChangedAsync(dto, cancellationToken);

        return dto;
    }
}
