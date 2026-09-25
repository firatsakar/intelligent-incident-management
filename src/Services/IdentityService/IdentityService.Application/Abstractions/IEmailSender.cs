namespace IdentityService.Application.Abstractions;

public sealed record OutgoingEmail(string To, string Subject, string Text, string Html);

/// <summary>
/// The platform's own mail: invitations and reset links. Not the customer's notification channels,
/// which NotificationService sends through the SMTP servers the customer configured.
/// </summary>
public interface IEmailSender
{
    /// <summary>Throws when the message could not be handed to the server.</summary>
    Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken = default);
}
