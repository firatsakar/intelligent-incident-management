using BuildingBlocks.SharedKernel;
using Microsoft.EntityFrameworkCore;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Domain.Aggregates;

namespace TelemetryIngestionService.Infrastructure.Persistence.Repositories;

public sealed class SourceCursorRepository : ISourceCursorRepository
{
    private readonly TelemetryDbContext _context;
    private readonly IOrganizationContext _organization;

    public SourceCursorRepository(TelemetryDbContext context, IOrganizationContext organization)
    {
        _context = context;
        _organization = organization;
    }

    public async Task<SourceCursor?> GetForPollingAsync(
        Guid telemetrySourceId,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .SourceCursors.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TelemetrySourceId == telemetrySourceId, cancellationToken);
    }

    public async Task<SourceCursor> GetOrCreateAsync(
        Guid telemetrySourceId,
        CancellationToken cancellationToken = default
    )
    {
        var cursor = await _context.SourceCursors.FirstOrDefaultAsync(
            x => x.TelemetrySourceId == telemetrySourceId,
            cancellationToken
        );

        if (cursor is not null)
            return cursor;

        // The scope was set by the polling loop from the source's own row before this ran, so the
        // cursor is born owned by whoever owns the source it tracks.
        cursor = SourceCursor.Start(_organization.Required, telemetrySourceId);

        await _context.SourceCursors.AddAsync(cursor, cancellationToken);

        return cursor;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
