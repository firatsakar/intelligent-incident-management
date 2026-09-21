using MediatR;
using NotificationService.Application.Abstractions;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Queries.GetIntegrations;

public sealed class GetIntegrationsQueryHandler
    : IRequestHandler<GetIntegrationsQuery, IReadOnlyList<IntegrationDto>>
{
    private readonly IIntegrationRepository _integrations;

    public GetIntegrationsQueryHandler(IIntegrationRepository integrations)
    {
        _integrations = integrations;
    }

    public async Task<IReadOnlyList<IntegrationDto>> Handle(
        GetIntegrationsQuery request,
        CancellationToken cancellationToken
    )
    {
        var integrations = await _integrations.GetAllAsync(cancellationToken);

        return integrations.Select(IntegrationDto.FromDomain).ToList();
    }
}
