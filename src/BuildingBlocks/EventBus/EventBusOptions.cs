namespace BuildingBlocks.EventBus;

public sealed class EventBusOptions
{
    public const string SectionName = "EventBus";
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public int RetryCount { get; set; } = 3;
    public string ExchangeName { get; set; } = "intelligent_incident_exchange";
}
