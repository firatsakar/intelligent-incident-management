using IncidentService.Application.Abstractions;
using IncidentService.Application.DTOs;
using MediatR;

namespace IncidentService.Application.Queries.GetIncidents;

public sealed class GetIncidentsQueryHandler
    : IRequestHandler<GetIncidentsQuery, PagedResult<IncidentDto>>
{
    private readonly IIncidentRepository _repository;

    public GetIncidentsQueryHandler(IIncidentRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<IncidentDto>> Handle(
        GetIncidentsQuery request,
        CancellationToken cancellationToken
    )
    {
        var (items, totalCount) = await _repository.GetPagedAsync(
            request.Status,
            request.Priority,
            request.PageNumber,
            request.PageSize,
            cancellationToken
        );

        var dtos = items.Select(IncidentDto.FromDomain).ToList();

        return new PagedResult<IncidentDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
        };
    }
}
