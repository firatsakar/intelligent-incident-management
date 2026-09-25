using MediatR;
using NotificationService.Application.Abstractions;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Exceptions;

namespace NotificationService.Application.Commands.SetIntegrationEnabled;

public sealed class SetIntegrationEnabledCommandHandler
    : IRequestHandler<SetIntegrationEnabledCommand, IntegrationDto>
{
    private readonly IIntegrationRepository _integrations;
    private readonly IRealtimeNotifier _realtime;

    public SetIntegrationEnabledCommandHandler(
        IIntegrationRepository integrations,
        IRealtimeNotifier realtime
    )
    {
        _integrations = integrations;
        _realtime = realtime;
    }

    public async Task<IntegrationDto> Handle(
        SetIntegrationEnabledCommand request,
        CancellationToken cancellationToken
    )
    {
        var integration =
            await _integrations.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new IntegrationNotFoundException(request.Id);

        if (request.IsEnabled)
            integration.Enable();
        else
            integration.Disable();

        _integrations.Update(integration);
        await _integrations.SaveChangesAsync(cancellationToken);

        // Worth pushing even though it is one boolean: a paused channel means nobody is being
        // told anything through it, and a second operator should not have to reload to find out.
        var dto = IntegrationDto.FromDomain(integration);
        await _realtime.IntegrationChangedAsync(dto, cancellationToken);

        return dto;
    }
}
