using MediatR;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Exceptions;

namespace NotificationService.Application.Commands.DeleteIntegration;

public sealed class DeleteIntegrationCommandHandler : IRequestHandler<DeleteIntegrationCommand>
{
    private readonly IIntegrationRepository _integrations;
    private readonly IRealtimeNotifier _realtime;

    public DeleteIntegrationCommandHandler(
        IIntegrationRepository integrations,
        IRealtimeNotifier realtime
    )
    {
        _integrations = integrations;
        _realtime = realtime;
    }

    public async Task Handle(DeleteIntegrationCommand request, CancellationToken cancellationToken)
    {
        var integration =
            await _integrations.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new IntegrationNotFoundException(request.Id);

        // Delivery history is deliberately left behind: it is an audit trail of what was sent,
        // and it should survive the integration being removed.
        _integrations.Remove(integration);
        await _integrations.SaveChangesAsync(cancellationToken);

        // Only the id is left to broadcast, which is why this is its own message rather than a
        // changed-with-a-flag — there is no DTO to carry any more.
        await _realtime.IntegrationDeletedAsync(integration.Id, cancellationToken);
    }
}
