using MediatR;
using NotificationService.Application.Abstractions;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Exceptions;

namespace NotificationService.Application.Commands.SetIntegrationEnabled;

public sealed class SetIntegrationEnabledCommandHandler
    : IRequestHandler<SetIntegrationEnabledCommand, IntegrationDto>
{
    private readonly IIntegrationRepository _integrations;

    public SetIntegrationEnabledCommandHandler(IIntegrationRepository integrations)
    {
        _integrations = integrations;
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

        return IntegrationDto.FromDomain(integration);
    }
}
