using MediatR;
using NotificationService.Application.Abstractions;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Aggregates;

namespace NotificationService.Application.Commands.CreateIntegration;

public sealed class CreateIntegrationCommandHandler
    : IRequestHandler<CreateIntegrationCommand, IntegrationDto>
{
    private readonly IIntegrationRepository _integrations;

    public CreateIntegrationCommandHandler(IIntegrationRepository integrations)
    {
        _integrations = integrations;
    }

    public async Task<IntegrationDto> Handle(
        CreateIntegrationCommand request,
        CancellationToken cancellationToken
    )
    {
        var integration = Integration.Create(
            request.Name,
            request.Channel,
            request.Config,
            request.MinPriority,
            request.CategoryFilter,
            request.IsEnabled
        );

        await _integrations.AddAsync(integration, cancellationToken);
        await _integrations.SaveChangesAsync(cancellationToken);

        return IntegrationDto.FromDomain(integration);
    }
}
