using MediatR;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Exceptions;

namespace NotificationService.Application.Commands.DeleteIntegration;

public sealed class DeleteIntegrationCommandHandler : IRequestHandler<DeleteIntegrationCommand>
{
    private readonly IIntegrationRepository _integrations;

    public DeleteIntegrationCommandHandler(IIntegrationRepository integrations)
    {
        _integrations = integrations;
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
    }
}
