using BuildingBlocks.Application;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using NotificationService.Application.Abstractions;
using NotificationService.Application.DTOs;
using NotificationService.Application.Validators;
using NotificationService.Domain.Exceptions;

namespace NotificationService.Application.Commands.UpdateIntegration;

public sealed class UpdateIntegrationCommandHandler
    : IRequestHandler<UpdateIntegrationCommand, IntegrationDto>
{
    private readonly IIntegrationRepository _integrations;

    public UpdateIntegrationCommandHandler(IIntegrationRepository integrations)
    {
        _integrations = integrations;
    }

    public async Task<IntegrationDto> Handle(
        UpdateIntegrationCommand request,
        CancellationToken cancellationToken
    )
    {
        var integration =
            await _integrations.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new IntegrationNotFoundException(request.Id);

        // Reads hand back "***" for anything that looks like a credential, so that is also the
        // only value an edit form can send for one. Restoring it here is what stops a save from
        // replacing the customer's SMTP password with the mask — a failure that would only
        // surface on the next real incident, as a notification that quietly did not arrive.
        //
        // Done before validation, so a restored secret counts as present.
        var config = ConfigMasking.Restore(request.Config, integration.Config);

        // The channel is fixed at creation, so the required keys can only be resolved once the
        // stored integration is in hand.
        var missing = IntegrationConfigRules.MissingKeys(integration.Channel, config);

        if (missing.Count > 0)
        {
            throw new ValidationException(
                [
                    new ValidationFailure(
                        nameof(UpdateIntegrationCommand.Config),
                        IntegrationConfigRules.Describe(integration.Channel, missing)
                    ),
                ]
            );
        }

        integration.Update(request.Name, request.MinPriority, request.CategoryFilter);
        integration.UpdateConfig(config);

        _integrations.Update(integration);
        await _integrations.SaveChangesAsync(cancellationToken);

        return IntegrationDto.FromDomain(integration);
    }
}
