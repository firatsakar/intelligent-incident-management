using BuildingBlocks.Web;
using IdentityService.Domain.Aggregates;
using IdentityService.Domain.Enums;
using IdentityService.Infrastructure.Security;
using Microsoft.IdentityModel.JsonWebTokens;

namespace IdentityService.Tests;

// What the other five processes will read out of a token, and the shape of the value this service
// stores instead of a refresh token.
public class JwtTokenGeneratorTests
{
    private readonly PlatformJwtOptions _options = new()
    {
        // Test-only, and long enough for HMAC-SHA256 to accept it.
        SigningKey = "a-test-signing-key-that-is-long-enough-for-hmac-sha256",
        Issuer = "iim",
        Audience = "iim",
        AccessMinutes = 15,
        RefreshDays = 14,
    };

    private readonly User _user = User.Create(
        Guid.NewGuid(),
        "ops@example.com",
        "Ops",
        "hash",
        UserRole.Admin
    );

    private JwtTokenGenerator Generator() => new(_options);

    public sealed class AccessTokens : JwtTokenGeneratorTests
    {
        [Fact]
        public void CarryTheClaimsEveryOtherServiceReads()
        {
            var token = new JsonWebTokenHandler().ReadJsonWebToken(
                Generator().CreateAccessToken(_user).Value
            );

            Assert.Equal(_user.Id.ToString(), token.GetClaim(JwtRegisteredClaimNames.Sub).Value);
            Assert.Equal(
                _user.OrganizationId.ToString(),
                token.GetClaim(PlatformClaims.Organization).Value
            );
            Assert.Equal("Admin", token.GetClaim(PlatformClaims.Role).Value);
            Assert.Equal("Ops", token.GetClaim(PlatformClaims.DisplayName).Value);
        }

        // Short names, not the SOAP-era URIs the older handler rewrites them into. Both sides read
        // "org" and there is no mapping step between them.
        [Fact]
        public void UseTheShortClaimNamesUnchanged()
        {
            var raw = Generator().CreateAccessToken(_user).Value;

            var token = new JsonWebTokenHandler().ReadJsonWebToken(raw);

            Assert.Contains(token.Claims, c => c.Type == "org");
            Assert.DoesNotContain(token.Claims, c => c.Type.StartsWith("http://schemas."));
        }

        [Fact]
        public void AreIssuedForTheConfiguredIssuerAndAudience()
        {
            var token = new JsonWebTokenHandler().ReadJsonWebToken(
                Generator().CreateAccessToken(_user).Value
            );

            Assert.Equal("iim", token.Issuer);
            Assert.Contains("iim", token.Audiences);
        }

        [Fact]
        public void ExpireWhenTheyClaimTo()
        {
            var issued = Generator().CreateAccessToken(_user);

            var token = new JsonWebTokenHandler().ReadJsonWebToken(issued.Value);

            // The two are written from separate calls to UtcNow, so they agree to the second
            // rather than to the tick.
            Assert.True((token.ValidTo - issued.ExpiresAt).Duration() < TimeSpan.FromSeconds(2));
            Assert.True(issued.ExpiresAt > DateTime.UtcNow.AddMinutes(14));
        }

        [Fact]
        public void AreUniquePerCall()
        {
            var generator = Generator();

            Assert.NotEqual(
                generator.CreateAccessToken(_user).Value,
                generator.CreateAccessToken(_user).Value
            );
        }
    }

    public sealed class RefreshTokens : JwtTokenGeneratorTests
    {
        [Fact]
        public void HashToTheSixtyFourLowercaseHexCharactersTheColumnHolds()
        {
            var pair = Generator().CreateRefreshToken();

            Assert.Equal(64, pair.Hash.Length);
            Assert.Matches("^[0-9a-f]{64}$", pair.Hash);
        }

        // The whole lookup depends on this: a presented value has to hash to the row that was
        // written when it was issued.
        [Fact]
        public void HashTheSameWayWhenPresentedBack()
        {
            var generator = Generator();
            var pair = generator.CreateRefreshToken();

            Assert.Equal(pair.Hash, generator.HashRefreshToken(pair.Value));
        }

        [Fact]
        public void AreNotTheirOwnHash()
        {
            var pair = Generator().CreateRefreshToken();

            Assert.NotEqual(pair.Value, pair.Hash);
        }

        [Fact]
        public void AreUniquePerCall()
        {
            var generator = Generator();

            Assert.NotEqual(generator.CreateRefreshToken().Value, generator.CreateRefreshToken().Value);
        }
    }
}
