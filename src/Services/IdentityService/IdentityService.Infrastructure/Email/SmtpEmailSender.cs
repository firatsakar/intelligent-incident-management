using IdentityService.Application.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace IdentityService.Infrastructure.Email;

// MailKit, as NotificationService's email channel uses — not a shared building block: that one
// sends through servers each customer configures, this one through the platform's own, and the two
// have nothing in common but the library.
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly MailOptions _options;

    public SmtpEmailSender(IOptions<MailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.From));
        message.To.Add(MailboxAddress.Parse(email.To));
        message.Subject = email.Subject;
        message.Body = new BodyBuilder { TextBody = email.Text, HtmlBody = email.Html }.ToMessageBody();

        using var client = new SmtpClient();

        await client.ConnectAsync(
            _options.Host,
            _options.Port,
            _options.UseTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None,
            cancellationToken
        );

        if (!string.IsNullOrEmpty(_options.Username))
            await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
