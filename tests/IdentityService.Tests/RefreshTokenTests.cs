using IdentityService.Domain.Aggregates;

namespace IdentityService.Tests;

// The only part of a session that can be taken away, so the interesting cases are all about a
// token that is no longer what it was.
public sealed class RefreshTokenTests
{
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    private static RefreshToken Given(TimeSpan? life = null) =>
        RefreshToken.Issue(Guid.NewGuid(), "hash-a", Now + (life ?? TimeSpan.FromDays(14)));

    public sealed class Activity
    {
        [Fact]
        public void AFreshTokenIsUsable()
        {
            Assert.True(Given().IsActive(Now));
        }

        [Fact]
        public void ExpiryEndsItWithoutAnythingBeingCalled()
        {
            var token = Given(TimeSpan.FromMinutes(5));

            Assert.False(token.IsActive(Now.AddMinutes(6)));
        }

        // The boundary is exclusive: a token is dead at its own expiry, not one tick after.
        [Fact]
        public void TheInstantOfExpiryIsAlreadyTooLate()
        {
            var token = Given(TimeSpan.FromMinutes(5));

            Assert.False(token.IsActive(Now.AddMinutes(5)));
        }
    }

    public sealed class Revocation
    {
        [Fact]
        public void RevokingEndsTheToken()
        {
            var token = Given();

            token.Revoke(Now);

            Assert.False(token.IsActive(Now));
            Assert.Equal(Now, token.RevokedAt);
        }

        // The first revocation is when the session actually ended. A later logout, or a sweep that
        // revokes everything a user holds, must not overwrite that with its own clock.
        [Fact]
        public void RevokingTwiceKeepsTheFirstTime()
        {
            var token = Given();

            token.Revoke(Now);
            token.Revoke(Now.AddHours(3));

            Assert.Equal(Now, token.RevokedAt);
        }
    }

    public sealed class Rotation
    {
        [Fact]
        public void RotatingRetiresThisTokenAndNamesItsSuccessor()
        {
            var token = Given();

            token.RotateTo("hash-b", Now);

            Assert.False(token.IsActive(Now));
            Assert.Equal(Now, token.RevokedAt);
            Assert.Equal("hash-b", token.ReplacedByHash);
        }

        // Presenting a token that has already been rotated is the reuse case: either a stolen copy
        // or a client that kept a stale one, and this row cannot tell which. Refusing here is what
        // forces that decision up to the caller, which ends the whole chain.
        [Fact]
        public void ASpentTokenCannotBeRotatedAgain()
        {
            var token = Given();
            token.RotateTo("hash-b", Now);

            Assert.Throws<InvalidOperationException>(() => token.RotateTo("hash-c", Now.AddMinutes(1)));

            Assert.Equal("hash-b", token.ReplacedByHash);
        }

        [Fact]
        public void AnExpiredTokenCannotBeRotated()
        {
            var token = Given(TimeSpan.FromMinutes(5));

            Assert.Throws<InvalidOperationException>(() => token.RotateTo("hash-b", Now.AddMinutes(6)));

            Assert.Null(token.ReplacedByHash);
            Assert.Null(token.RevokedAt);
        }

        [Fact]
        public void ARevokedTokenCannotBeRotated()
        {
            var token = Given();
            token.Revoke(Now);

            Assert.Throws<InvalidOperationException>(() => token.RotateTo("hash-b", Now));

            Assert.Null(token.ReplacedByHash);
        }
    }
}
