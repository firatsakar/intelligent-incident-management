using System.Net;
using IdentityService.Domain.Enums;

namespace IdentityService.Application.Accounts;

/// <summary>
/// The two emails the platform sends about accounts. English and Turkish in one message: the
/// console speaks both, and nothing yet records which one a person who has no account prefers.
/// </summary>
public static class AccountEmails
{
    public static Application.Abstractions.OutgoingEmail Invitation(
        string to,
        string organization,
        UserRole role,
        string link,
        DateTime expiresAt
    )
    {
        var expires = expiresAt.ToString("yyyy-MM-dd HH:mm 'UTC'");

        var text = $"""
            You have been invited to join {organization} on IIM as {role}.
            Open this link to choose your name and password: {link}
            The link works once and expires on {expires}.

            {organization} organizasyonuna IIM'de {role} olarak katılmaya davet edildiniz.
            Adınızı ve parolanızı belirlemek için bu bağlantıyı açın: {link}
            Bağlantı bir kez çalışır ve {expires} tarihinde geçersiz olur.
            """;

        return new(to, $"Invitation to {organization} on IIM / IIM'de {organization} daveti", text, Html(text, link));
    }

    public static Application.Abstractions.OutgoingEmail PasswordReset(
        string to,
        string organization,
        string link,
        DateTime expiresAt
    )
    {
        var expires = expiresAt.ToString("yyyy-MM-dd HH:mm 'UTC'");

        var text = $"""
            An Admin of {organization} has issued you a link to set a new password on IIM: {link}
            The link works once and expires on {expires}. Your current sessions end when you use it.

            {organization} organizasyonunun bir Admin'i IIM'de yeni parola belirlemeniz için bir bağlantı üretti: {link}
            Bağlantı bir kez çalışır ve {expires} tarihinde geçersiz olur. Kullandığınızda açık oturumlarınız kapanır.
            """;

        return new(to, "IIM password reset / IIM parola sıfırlama", text, Html(text, link));
    }

    // The same words as the text part, with the link clickable. Nothing else: an email about an
    // account should not look like marketing, and should not need images to be read.
    private static string Html(string text, string link)
    {
        var encoded = WebUtility.HtmlEncode(text).Replace(
            WebUtility.HtmlEncode(link),
            $"<a href=\"{WebUtility.HtmlEncode(link)}\">{WebUtility.HtmlEncode(link)}</a>"
        );

        return $"<p style=\"font-family:sans-serif;white-space:pre-line\">{encoded}</p>";
    }
}
