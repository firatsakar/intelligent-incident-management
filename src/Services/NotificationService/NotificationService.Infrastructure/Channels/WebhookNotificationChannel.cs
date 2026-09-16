using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Abstractions;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Aggregates;
using NotificationService.Domain.Enums;
using Polly;
using Polly.Retry;

namespace NotificationService.Infrastructure.Channels;

// Required settings: Url. Optional: TimeoutSeconds, plus any number of "Header:<name>" entries
// that are sent verbatim — that is how a customer passes an API key or a signing token.
public sealed class WebhookNotificationChannel : INotificationChannel
{
    public const string HttpClientName = "webhook-notifications";

    private const string HeaderSettingPrefix = "Header:";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    // Transient failures only: a 4xx means the customer's endpoint rejected the payload and
    // retrying would just repeat the rejection.
    private static readonly ResiliencePipeline<HttpResponseMessage> RetryPipeline =
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(
                new RetryStrategyOptions<HttpResponseMessage>
                {
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .Handle<TaskCanceledException>()
                        .HandleResult(response =>
                            (int)response.StatusCode >= 500
                            || response.StatusCode == HttpStatusCode.RequestTimeout
                            || response.StatusCode == HttpStatusCode.TooManyRequests
                        ),
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Exponential,
                }
            )
            .Build();

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookNotificationChannel> _logger;

    public WebhookNotificationChannel(
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookNotificationChannel> logger
    )
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public NotificationChannelType Channel => NotificationChannelType.Webhook;

    public async Task<DeliveryResult> SendAsync(
        NotificationMessage message,
        Integration integration,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var url = integration.RequiredSetting("Url");

            var client = _httpClientFactory.CreateClient(HttpClientName);
            client.Timeout = ResolveTimeout(integration);

            using var response = await RetryPipeline.ExecuteAsync(
                async token =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = JsonContent.Create(message),
                    };

                    foreach (var (name, value) in ResolveHeaders(integration))
                    {
                        request.Headers.TryAddWithoutValidation(name, value);
                    }

                    return await client.SendAsync(request, token);
                },
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogWarning(
                    "Webhook notification for incident {IncidentId} through integration {IntegrationName} returned {StatusCode}.",
                    message.IncidentId,
                    integration.Name,
                    (int)response.StatusCode
                );

                return DeliveryResult.Failure(
                    $"Webhook returned {(int)response.StatusCode} {response.ReasonPhrase}. {Truncate(body)}"
                );
            }

            _logger.LogInformation(
                "Webhook notification sent for incident {IncidentId} through integration {IntegrationName}.",
                message.IncidentId,
                integration.Name
            );

            return DeliveryResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Webhook notification failed for incident {IncidentId} through integration {IntegrationName}.",
                message.IncidentId,
                integration.Name
            );

            return DeliveryResult.Failure(ex.Message);
        }
    }

    private static TimeSpan ResolveTimeout(Integration integration)
    {
        var configured = integration.OptionalSetting("TimeoutSeconds");

        return configured is not null && int.TryParse(configured, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : DefaultTimeout;
    }

    private static IEnumerable<(string Name, string Value)> ResolveHeaders(Integration integration)
    {
        return integration
            .Config.Where(pair =>
                pair.Key.StartsWith(HeaderSettingPrefix, StringComparison.OrdinalIgnoreCase)
            )
            .Select(pair => (pair.Key[HeaderSettingPrefix.Length..], pair.Value));
    }

    private static string Truncate(string body)
    {
        const int limit = 512;

        return body.Length <= limit ? body : body[..limit];
    }
}
