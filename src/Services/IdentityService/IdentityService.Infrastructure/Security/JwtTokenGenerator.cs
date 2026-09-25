using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Web;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.Aggregates;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Infrastructure.Security;

public sealed class JwtTokenGenerator : ITokenGenerator
{
    private readonly PlatformJwtOptions _options;

    public JwtTokenGenerator(PlatformJwtOptions options)
    {
        _options = options;
    }

    public AccessToken CreateAccessToken(User user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_options.AccessMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expires,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),

                // A unique id per token. Nothing reads it yet; it is what a future "which session
                // was that" question would be answered from, and it costs nothing to mint.
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),

                // The claim every query filter in the platform will read. It is in the token
                // rather than looked up per request because the four other services cannot read
                // this database.
                [PlatformClaims.Organization] = user.OrganizationId.ToString(),
                [PlatformClaims.Role] = user.Role.ToString(),
                [PlatformClaims.DisplayName] = user.DisplayName,
            },
            SigningCredentials = new SigningCredentials(
                _options.Key(),
                SecurityAlgorithms.HmacSha256
            ),
        };

        // JsonWebTokenHandler rather than the older JwtSecurityTokenHandler: it does not rewrite
        // short claim names into SOAP-era URIs, so "org" stays "org" on both sides.
        return new AccessToken(new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }

    public RefreshTokenPair CreateRefreshToken()
    {
        // 256 bits from the OS. This value is the entire secret — there is no password behind it
        // to fall back on — so it is generated rather than derived from anything.
        var value = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

        return new RefreshTokenPair(
            value,
            HashRefreshToken(value),
            DateTime.UtcNow.AddDays(_options.RefreshDays)
        );
    }

    // Plain SHA-256, and the reasoning is in RefreshToken: there is nothing to guess in 256 random
    // bits, and the row has to be found by this value, which a per-row salt would prevent.
    // Lowercase hex, 64 characters, which is what the column is sized for.
    public string HashRefreshToken(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
