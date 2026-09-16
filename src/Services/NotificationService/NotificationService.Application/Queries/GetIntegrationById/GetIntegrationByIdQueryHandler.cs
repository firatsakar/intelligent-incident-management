using MediatR;
using NotificationService.Application.Abstractions;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Exceptions;

namespace NotificationService.Application.Queries.GetIntegrationById;

public sealed class GetIntegrationByIdQueryHandler
    : IRequestHandler<GetIntegrationByIdQuery, IntegrationDto>
{
    private readonly IIntegrationRepository _integrations;

    public GetIntegrationByIdQueryHandler(IIntegrationRepository integrations)
    {
        _integrations = integrations;
    }

    public async Task<IntegrationDto> Handle(
        GetIntegrationByIdQuery request,
        CancellationToken cancellationToken
    )
    {
        var integration =
            await _integrations.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new IntegrationNotFoundException(request.Id);

        return IntegrationDto.FromDomain(integration);
    }
}
