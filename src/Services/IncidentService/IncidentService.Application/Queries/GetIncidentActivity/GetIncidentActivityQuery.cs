using IncidentService.Application.Abstractions;
using IncidentService.Application.DTOs;
using IncidentService.Domain.Exceptions;
using MediatR;

namespace IncidentService.Application.Queries.GetIncidentActivity;

/// <summary>What an incident went through, oldest first.</summary>
public sealed record GetIncidentActivityQuery(Guid IncidentId) : IRequest<IReadOnlyList<IncidentActivityDto>>;

public sealed class GetIncidentActivityQueryHandler
    : IRequestHandler<GetIncidentActivityQuery, IReadOnlyList<IncidentActivityDto>>
{
    // A bound, not a page. Past this the oldest rows drop off the read; nothing is deleted.
    public const int Limit = 500;

    private readonly IIncidentRepository _incidents;
    private readonly IIncidentActivityRepository _activity;

    public GetIncidentActivityQueryHandler(IIncidentRepository incidents, IIncidentActivityRepository activity)
    {
        _incidents = incidents;
        _activity = activity;
    }

    public async Task<IReadOnlyList<IncidentActivityDto>> Handle(
        GetIncidentActivityQuery request,
        CancellationToken cancellationToken
    )
    {
        // Asked of the incident first, so an id from another organisation — or none — is a 404 and
        // not an empty history, which would say the incident exists.
        _ = await _incidents.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new IncidentNotFoundException(request.IncidentId);

        var rows = await _activity.ListForIncidentAsync(request.IncidentId, Limit, cancellationToken);

        return rows.Select(IncidentActivityDto.FromDomain).ToList();
    }
}
