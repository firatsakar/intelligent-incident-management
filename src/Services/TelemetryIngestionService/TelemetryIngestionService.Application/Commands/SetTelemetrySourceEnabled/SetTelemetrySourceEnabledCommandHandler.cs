using MediatR;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Domain.Exceptions;

namespace TelemetryIngestionService.Application.Commands.SetTelemetrySourceEnabled;

public sealed class SetTelemetrySourceEnabledCommandHandler
    : IRequestHandler<SetTelemetrySourceEnabledCommand, TelemetrySourceDto>
{
    private readonly ITelemetrySourceRepository _sources;

    public SetTelemetrySourceEnabledCommandHandler(ITelemetrySourceRepository sources)
    {
        _sources = sources;
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

        return TelemetrySourceDto.FromDomain(source);
    }
}
