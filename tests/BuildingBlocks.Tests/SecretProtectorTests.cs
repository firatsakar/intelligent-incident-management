using System.Security.Cryptography;
using BuildingBlocks.Application;
using BuildingBlocks.SharedKernel;

namespace BuildingBlocks.Tests;

// Customer credentials are encrypted in the database. What goes in comes back out; what
// is altered, or read with another key, does not come back as something else.
public sealed class SecretProtectorTests
{
    private static readonly SecretProtector Secrets = new(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void AValueComesBackAsItWentIn()
    {
        var stored = Secrets.Protect("smtp-p@ss wörd");

        Assert.StartsWith(SecretProtector.Prefix, stored);
        Assert.DoesNotContain("smtp-p@ss", stored);
        Assert.Equal("smtp-p@ss wörd", Secrets.Unprotect(stored));
    }

    [Fact]
    public void TheSameValueTwiceIsTwoDifferentCiphertexts() =>
        Assert.NotEqual(Secrets.Protect("token"), Secrets.Protect("token"));

    [Fact]
    public void EmptyStaysEmptyAndAnEncryptedValueIsNotEncryptedAgain()
    {
        Assert.Equal("", Secrets.Protect(""));

        var once = Secrets.Protect("token");
        Assert.Equal(once, Secrets.Protect(once));
    }

    [Fact]
    public void AValueFromBeforeEncryptionIsReadAsItIs() =>
        Assert.Equal("legacy-plain-token", Secrets.Unprotect("legacy-plain-token"));

    [Fact]
    public void AnAlteredValueDoesNotDecrypt()
    {
        var stored = Secrets.Protect("token").ToCharArray();
        var last = stored.Length - 3;
        stored[last] = stored[last] == 'A' ? 'B' : 'A';

        Assert.ThrowsAny<CryptographicException>(() => Secrets.Unprotect(new string(stored)));
    }

    [Fact]
    public void AnotherKeyCannotReadIt()
    {
        var other = new SecretProtector(RandomNumberGenerator.GetBytes(32));

        Assert.ThrowsAny<CryptographicException>(() => other.Unprotect(Secrets.Protect("token")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("c2hvcnQ=")]
    public void AMissingOrMalformedKeyStopsTheServiceWithTheReason(string? configured)
    {
        var refused = Assert.Throws<InvalidOperationException>(() => SecretProtector.FromConfiguration(configured));

        Assert.Contains(SecretProtector.ConfigurationKey, refused.Message);
    }

    [Fact]
    public void AConfiguredKeyIsUsed()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var a = SecretProtector.FromConfiguration(key);
        var b = SecretProtector.FromConfiguration(key);

        Assert.Equal("token", b.Unprotect(a.Protect("token")));
    }

    // ---- the config bags ---------------------------------------------------------------------

    [Fact]
    public void OnlyTheCredentialsInAConfigAreEncrypted()
    {
        var config = new Dictionary<string, string>
        {
            ["Host"] = "smtp.example.com",
            ["Password"] = "hunter2",
            ["ApiToken"] = "jira-token",
        };

        var stored = ConfigSecrets.Protect(config, Secrets);

        Assert.Equal("smtp.example.com", stored["Host"]);
        Assert.StartsWith(SecretProtector.Prefix, stored["Password"]);
        Assert.StartsWith(SecretProtector.Prefix, stored["ApiToken"]);
        Assert.Equal(config, ConfigSecrets.Unprotect(stored, Secrets));
    }

    [Fact]
    public void AConfigStillHoldingAPlainCredentialIsFound()
    {
        Assert.True(ConfigSecrets.HasPlaintextSecret(new Dictionary<string, string> { ["Password"] = "hunter2" }));
        Assert.False(
            ConfigSecrets.HasPlaintextSecret(ConfigSecrets.Protect(new Dictionary<string, string> { ["Password"] = "hunter2" }, Secrets))
        );
        Assert.False(ConfigSecrets.HasPlaintextSecret(new Dictionary<string, string> { ["Url"] = "https://x", ["Password"] = "" }));
    }
}
