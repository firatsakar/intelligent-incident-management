namespace NotificationService.Application.DTOs;

public sealed record DeliveryResult
{
    public required bool IsSuccess { get; init; }
    public string? Error { get; init; }

    public static DeliveryResult Success() => new() { IsSuccess = true };

    public static DeliveryResult Failure(string error) => new() { IsSuccess = false, Error = error };
}
