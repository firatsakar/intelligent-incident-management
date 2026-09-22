using MediatR;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Domain.Exceptions;

namespace TelemetryIngestionService.Application.Commands.DeleteTelemetrySource;

public sealed class DeleteTelemetrySourceCommandHandler
    : IRequestHandler<DeleteTelemetrySourceCommand>
{
    private readonly ITelemetrySourceRepository _sources;
    private readonly IRealtimeNotifier _realtime;

    public DeleteTelemetrySourceCommandHandler(
        ITelemetrySourceRepository sources,
        IRealtimeNotifier realtime
    )
    {
        _sources = sources;
        _realtime = realtime;
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

        // Only the id survives a delete, so this is its own message rather than a
        // changed-with-a-flag: there is no DTO left to carry.
        await _realtime.SourceDeletedAsync(source.Id, cancellationToken);
    }
}
