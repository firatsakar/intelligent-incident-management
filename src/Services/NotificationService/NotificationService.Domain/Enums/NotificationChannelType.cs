using System.Text.Json.Serialization;

namespace NotificationService.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationChannelType
{
    Email,
    Webhook,
    Jira
}
