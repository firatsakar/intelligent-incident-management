namespace NotificationService.API.Contracts;

public sealed record SetIntegrationEnabledRequest
{
    public required bool IsEnabled { get; init; }
}
