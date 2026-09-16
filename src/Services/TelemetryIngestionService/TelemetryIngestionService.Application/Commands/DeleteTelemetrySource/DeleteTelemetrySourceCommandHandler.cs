using MediatR;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Domain.Exceptions;

namespace TelemetryIngestionService.Application.Commands.DeleteTelemetrySource;

public sealed class DeleteTelemetrySourceCommandHandler
    : IRequestHandler<DeleteTelemetrySourceCommand>
{
    private readonly ITelemetrySourceRepository _sources;

    public DeleteTelemetrySourceCommandHandler(ITelemetrySourceRepository sources)
    {
        _sources = sources;
    }

    public async Task Handle(
        DeleteTelemetrySourceCommand request,
        CancellationToken cancellationToken
    )
    {
        var source =
            await _sources.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new TelemetrySourceNotFoundException(request.Id);

        // Ingested logs and signatures are deliberately left behind: they are the evidence trail
        // for incidents that were already opened from this source.
        _sources.Remove(source);
        await _sources.SaveChangesAsync(cancellationToken);
    }
}
