using System.Globalization;
using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using NotificationService.Application.Abstractions;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Aggregates;
using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Channels;

// Required settings: Host, Port, From, To (comma-separated). Optional: UserName, Password.
public sealed class EmailNotificationChannel : INotificationChannel
{
    private readonly ILogger<EmailNotificationChannel> _logger;

    public EmailNotificationChannel(ILogger<EmailNotificationChannel> logger)
    {
        _logger = logger;
    }

    public NotificationChannelType Channel => NotificationChannelType.Email;

    public async Task<DeliveryResult> SendAsync(
        NotificationMessage message,
        Integration integration,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var mail = BuildMail(message, integration);

            using var client = new SmtpClient();

            // Auto negotiates STARTTLS only when the server advertises it, which keeps plain
            // dev relays like Mailpit working without a per-integration TLS flag.
            await client.ConnectAsync(
                integration.RequiredSetting("Host"),
                integration.RequiredIntSetting("Port"),
                SecureSocketOptions.Auto,
                cancellationToken
            );

            var userName = integration.OptionalSetting("UserName");
            var password = integration.OptionalSetting("Password");

            if (userName is not null && password is not null)
            {
                await client.AuthenticateAsync(userName, password, cancellationToken);
            }

            await client.SendAsync(mail, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            _logger.LogInformation(
                "Email notification sent for incident {IncidentId} through integration {IntegrationName}.",
                message.IncidentId,
                integration.Name
            );

            return DeliveryResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Email notification failed for incident {IncidentId} through integration {IntegrationName}.",
                message.IncidentId,
                integration.Name
            );

            return DeliveryResult.Failure(ex.Message);
        }
    }

    private static MimeMessage BuildMail(NotificationMessage message, Integration integration)
    {
        var mail = new MimeMessage
        {
            Subject = $"[{message.SuggestedPriority}] {message.IncidentTitle}",
        };

        mail.From.Add(MailboxAddress.Parse(integration.RequiredSetting("From")));

        var recipients = integration
            .RequiredSetting("To")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var recipient in recipients)
        {
            mail.To.Add(MailboxAddress.Parse(recipient));
        }

        mail.Body = new BodyBuilder
        {
            TextBody = BuildTextBody(message),
            HtmlBody = BuildHtmlBody(message),
        }.ToMessageBody();

        return mail;
    }

    private static string BuildTextBody(NotificationMessage message)
    {
        var body = new StringBuilder();

        body.AppendLine(message.IncidentTitle);
        body.AppendLine();
        body.AppendLine($"Incident:   {message.IncidentId}");
        body.AppendLine($"Priority:   {message.SuggestedPriority} (AI suggested)");
        body.AppendLine($"Category:   {message.SuggestedCategory}");
        body.AppendLine($"Confidence: {FormatConfidence(message.Confidence)}");
        body.AppendLine($"Analyzed:   {message.AnalyzedAt:u}");
        body.AppendLine();
        body.AppendLine("Reasoning");
        body.AppendLine(message.Reasoning);

        return body.ToString();
    }

    private static string BuildHtmlBody(NotificationMessage message)
    {
        return $"""
            <h2>{Escape(message.IncidentTitle)}</h2>
            <table cellpadding="4">
              <tr><td><b>Incident</b></td><td>{message.IncidentId}</td></tr>
              <tr><td><b>Priority</b></td><td>{Escape(message.SuggestedPriority)} (AI suggested)</td></tr>
              <tr><td><b>Category</b></td><td>{Escape(message.SuggestedCategory)}</td></tr>
              <tr><td><b>Confidence</b></td><td>{FormatConfidence(message.Confidence)}</td></tr>
              <tr><td><b>Analyzed</b></td><td>{message.AnalyzedAt:u}</td></tr>
            </table>
            <h3>Reasoning</h3>
            <p>{Escape(message.Reasoning)}</p>
            """;
    }

    private static string FormatConfidence(double? confidence)
    {
        return confidence.HasValue
            ? confidence.Value.ToString("P0", CultureInfo.InvariantCulture)
            : "n/a";
    }

    private static string Escape(string value)
    {
        return System.Net.WebUtility.HtmlEncode(value);
    }
}
