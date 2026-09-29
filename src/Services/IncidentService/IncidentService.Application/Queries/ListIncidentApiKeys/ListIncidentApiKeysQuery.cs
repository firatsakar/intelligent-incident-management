using IncidentService.Application.Abstractions;
using IncidentService.Application.DTOs;
using MediatR;

namespace IncidentService.Application.Queries.ListIncidentApiKeys;

public sealed record ListIncidentApiKeysQuery : IRequest<IReadOnlyList<IncidentApiKeyDto>>;

public sealed class ListIncidentApiKeysQueryHandler
    : IRequestHandler<ListIncidentApiKeysQuery, IReadOnlyList<IncidentApiKeyDto>>
{
    private readonly IIncidentApiKeyRepository _keys;

    public ListIncidentApiKeysQueryHandler(IIncidentApiKeyRepository keys)
    {
        _keys = keys;
    }

    public async Task<IReadOnlyList<IncidentApiKeyDto>> Handle(
        ListIncidentApiKeysQuery request,
        CancellationToken cancellationToken
    ) => (await _keys.ListAsync(cancellationToken)).Select(IncidentApiKeyDto.FromDomain).ToList();
}
