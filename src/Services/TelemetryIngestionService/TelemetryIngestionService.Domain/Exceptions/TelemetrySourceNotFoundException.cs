using BuildingBlocks.SharedKernel.Exceptions;

namespace TelemetryIngestionService.Domain.Exceptions;

public sealed class TelemetrySourceNotFoundException : NotFoundException
{
    public TelemetrySourceNotFoundException(Guid id)
        : base($"Telemetry source with id '{id}' was not found.") { }
}
