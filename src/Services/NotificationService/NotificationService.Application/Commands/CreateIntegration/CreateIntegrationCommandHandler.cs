using MediatR;
using NotificationService.Application.Abstractions;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Aggregates;

namespace NotificationService.Application.Commands.CreateIntegration;

public sealed class CreateIntegrationCommandHandler
    : IRequestHandler<CreateIntegrationCommand, IntegrationDto>
{
    private readonly IIntegrationRepository _integrations;
    private readonly IRealtimeNotifier _realtime;

    public CreateIntegrationCommandHandler(
        IIntegrationRepository integrations,
        IRealtimeNotifier realtime
    )
    {
        _integrations = integrations;
        _realtime = realtime;
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

        // After the save, never before: what is broadcast has to be what is stored.
        var dto = IntegrationDto.FromDomain(integration);
        await _realtime.IntegrationChangedAsync(dto, cancellationToken);

        return dto;
    }
}
