using IdentityService.Domain.Aggregates;

namespace IdentityService.Application.Abstractions;

/// <summary>A signed access token and the moment it stops being one.</summary>
public sealed record AccessToken(string Value, DateTime ExpiresAt);

/// <summary>
/// A refresh token in both of the forms it exists in: the value the browser keeps, and the hash
/// that is all this service stores of it.
/// </summary>
public sealed record RefreshTokenPair(string Value, string Hash, DateTime ExpiresAt);

public interface ITokenGenerator
{
    /// <summary>
    /// Mints the token the other four services will validate on their own. Nothing can withdraw
    /// it before it expires, which is why it expires quickly.
    /// </summary>
    AccessToken CreateAccessToken(User user);

    RefreshTokenPair CreateRefreshToken();

    /// <summary>
    /// The same hash <see cref="CreateRefreshToken"/> produced, for turning what a browser
    /// presented back into the row it belongs to.
    /// </summary>
    string HashRefreshToken(string value);
}
