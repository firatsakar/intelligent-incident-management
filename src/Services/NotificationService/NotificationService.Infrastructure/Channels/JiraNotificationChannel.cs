using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Abstractions;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Aggregates;
using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Channels;

// Creates an issue through the Jira Cloud REST API.
// Required settings: BaseUrl, ProjectKey, Email, ApiToken. Optional: IssueType (default Task).
public sealed class JiraNotificationChannel : INotificationChannel
{
    public const string HttpClientName = "jira-notifications";

    private const string DefaultIssueType = "Task";
    private const string CreateIssuePath = "rest/api/3/issue";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JiraNotificationChannel> _logger;

    public JiraNotificationChannel(
        IHttpClientFactory httpClientFactory,
        ILogger<JiraNotificationChannel> logger
    )
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public NotificationChannelType Channel => NotificationChannelType.Jira;

    public async Task<DeliveryResult> SendAsync(
        NotificationMessage message,
        Integration integration,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var baseUrl = integration.RequiredSetting("BaseUrl").TrimEnd('/');

            var client = _httpClientFactory.CreateClient(HttpClientName);

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{baseUrl}/{CreateIssuePath}"
            )
            {
                Content = JsonContent.Create(BuildIssuePayload(message, integration)),
            };

            request.Headers.Authorization = BuildBasicAuth(integration);

            using var response = await client.SendAsync(request, cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Jira notification for incident {IncidentId} through integration {IntegrationName} returned {StatusCode}.",
                    message.IncidentId,
                    integration.Name,
                    (int)response.StatusCode
                );

                return DeliveryResult.Failure(
                    $"Jira returned {(int)response.StatusCode} {response.ReasonPhrase}. {Truncate(body)}"
                );
            }

            _logger.LogInformation(
                "Jira issue {IssueKey} created for incident {IncidentId} through integration {IntegrationName}.",
                ReadIssueKey(body),
                message.IncidentId,
                integration.Name
            );

            return DeliveryResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Jira notification failed for incident {IncidentId} through integration {IntegrationName}.",
                message.IncidentId,
                integration.Name
            );

            return DeliveryResult.Failure(ex.Message);
        }
    }

    private static AuthenticationHeaderValue BuildBasicAuth(Integration integration)
    {
        var credentials =
            $"{integration.RequiredSetting("Email")}:{integration.RequiredSetting("ApiToken")}";

        return new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials))
        );
    }

    private static object BuildIssuePayload(NotificationMessage message, Integration integration)
    {
        return new
        {
            fields = new
            {
                project = new { key = integration.RequiredSetting("ProjectKey") },
                issuetype = new
                {
                    name = integration.OptionalSetting("IssueType") ?? DefaultIssueType,
                },
                summary = $"[{message.SuggestedPriority}] {message.IncidentTitle}",
                description = BuildAtlassianDocument(message),
            },
        };
    }

    // Jira Cloud's v3 API rejects a plain string description — it wants Atlassian Document Format,
    // so the body is assembled as an ADF document of paragraphs.
    private static object BuildAtlassianDocument(NotificationMessage message)
    {
        string[] paragraphs =
        [
            $"Incident: {message.IncidentId}",
            $"AI suggested priority: {message.SuggestedPriority}",
            $"AI suggested category: {message.SuggestedCategory}",
            $"Confidence: {FormatConfidence(message.Confidence)}",
            $"Analyzed at: {message.AnalyzedAt:u}",
            "Reasoning",
            message.Reasoning,
        ];

        return new
        {
            type = "doc",
            version = 1,
            content = paragraphs
                .Select(text => new
                {
                    type = "paragraph",
                    content = new[] { new { type = "text", text } },
                })
                .ToArray(),
        };
    }

    private static string FormatConfidence(double? confidence)
    {
        return confidence.HasValue
            ? confidence.Value.ToString("P0", CultureInfo.InvariantCulture)
            : "n/a";
    }

    private static string ReadIssueKey(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            return document.RootElement.TryGetProperty("key", out var key)
                ? key.GetString() ?? "unknown"
                : "unknown";
        }
        catch (JsonException)
        {
            return "unknown";
        }
    }

    private static string Truncate(string body)
    {
        const int limit = 512;

        return body.Length <= limit ? body : body[..limit];
    }
}
