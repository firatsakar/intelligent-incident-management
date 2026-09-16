using MediatR;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Abstractions;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Commands.SendTestNotification;

public sealed class SendTestNotificationCommandHandler
    : IRequestHandler<SendTestNotificationCommand, DeliveryResult?>
{
    private readonly IIntegrationRepository _integrations;
    private readonly INotificationChannelResolver _channels;
    private readonly ILogger<SendTestNotificationCommandHandler> _logger;

    public SendTestNotificationCommandHandler(
        IIntegrationRepository integrations,
        INotificationChannelResolver channels,
        ILogger<SendTestNotificationCommandHandler> logger
    )
    {
        _integrations = integrations;
        _channels = channels;
        _logger = logger;
    }

    public async Task<DeliveryResult?> Handle(
        SendTestNotificationCommand request,
        CancellationToken cancellationToken
    )
    {
        var integration = await _integrations.GetByIdAsync(request.IntegrationId, cancellationToken);

        if (integration is null)
            return null;

        _logger.LogInformation(
            "Sending a test notification through integration {IntegrationName} ({Channel}).",
            integration.Name,
            integration.Channel
        );

        var channel = _channels.Resolve(integration.Channel);

        return await channel.SendAsync(BuildSampleMessage(), integration, cancellationToken);
    }

    private static NotificationMessage BuildSampleMessage()
    {
        return new NotificationMessage
        {
            IncidentId = Guid.Empty,
            IncidentTitle = "Test notification from the incident management platform",
            SuggestedPriority = "High",
            SuggestedCategory = "Infrastructure",
            Reasoning =
                "This is a sample notification sent to verify the integration settings. No real incident is involved.",
            Confidence = 0.95,
            AnalyzedAt = DateTime.UtcNow,
        };
    }
}
