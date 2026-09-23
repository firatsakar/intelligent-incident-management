using IdentityService.Application.Abstractions;
using IdentityService.Infrastructure.Security;

namespace IdentityService.Tests;

public sealed class PasswordHasherTests
{
    private readonly IPasswordHasher _hasher = new BCryptPasswordHasher();

    [Fact]
    public void APasswordVerifiesAgainstItsOwnHash()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.True(_hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void AnythingElseDoesNot()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.False(_hasher.Verify("correct horse battery stapl", hash));
    }

    [Fact]
    public void ComparisonIsCaseSensitive()
    {
        var hash = _hasher.Hash("Passphrase");

        Assert.False(_hasher.Verify("passphrase", hash));
    }

    // Two people with the same password must not have the same row. Without a per-hash salt a
    // leaked table tells you which accounts share a password before you have cracked any of them.
    [Fact]
    public void TheSamePasswordHashesDifferentlyEveryTime()
    {
        var first = _hasher.Hash("shared");
        var second = _hasher.Hash("shared");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify("shared", first));
        Assert.True(_hasher.Verify("shared", second));
    }

    // The work factor is the number the whole scheme rests on, and it is encoded in the hash
    // itself — "$2a$12$…". Asserting it here means lowering it cannot pass unnoticed as a
    // performance tweak.
    [Fact]
    public void TheWorkFactorIsTwelve()
    {
        Assert.StartsWith("$2a$12$", _hasher.Hash("anything"));
    }

    // A stored value this library cannot read is a sign-in that fails, not a request that errors.
    // The caller asked whether this password matches; the answer is no.
    [Theory]
    [InlineData("")]
    [InlineData("not a hash")]
    [InlineData("$2a$12$tooshort")]
    public void AnUnreadableStoredValueIsAFailedSignInRatherThanAnException(string stored)
    {
        Assert.False(_hasher.Verify("anything", stored));
    }
}
