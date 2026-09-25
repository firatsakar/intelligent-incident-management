using IdentityService.Domain.Aggregates;
using IdentityService.Domain.Enums;
using IdentityService.Domain.Services;

namespace IdentityService.Tests;

// A one-time link is a way into an account. Every interesting case is about the link no longer
// being one: used, withdrawn, replaced, or simply too old.
public sealed class InvitationAndResetTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    public sealed class Tokens
    {
        [Fact]
        public void OnlyTheHashIsMeantToBeKept()
        {
            var issued = OneTimeToken.Generate();

            Assert.Equal(OneTimeToken.Hash(issued.Token), issued.Hash);
            Assert.Equal(64, issued.Hash.Length);
            Assert.DoesNotContain(issued.Token, issued.Hash);
        }

        [Fact]
        public void TwoLinksAreNeverTheSame()
        {
            Assert.NotEqual(OneTimeToken.Generate().Token, OneTimeToken.Generate().Token);
        }

        [Fact]
        public void TheTokenSurvivesAUrlUntouched()
        {
            // It travels in a link path; '+', '/' and '=' would each need escaping somewhere.
            Assert.All(Enumerable.Range(0, 50).Select(_ => OneTimeToken.Generate().Token), token =>
                Assert.Matches("^[A-Za-z0-9_-]+$", token)
            );
        }
    }

    public sealed class Invitations
    {
        private static Invitation Given() =>
            Invitation.Issue(Guid.NewGuid(), "  New.Person@Example.COM ", UserRole.Engineer, "hash", Guid.NewGuid(), Now);

        [Fact]
        public void TheAddressIsStoredTheWayAnAccountsIs()
        {
            // So the account it becomes, and a second invitation to the same person, both match it.
            Assert.Equal(User.Normalize("New.Person@Example.COM"), Given().Email);
        }

        [Fact]
        public void ItIsPendingForSevenDaysAndNotAMomentLonger()
        {
            var invitation = Given();

            Assert.True(invitation.IsPending(Now + TimeSpan.FromDays(7) - TimeSpan.FromSeconds(1)));
            Assert.False(invitation.IsPending(Now + TimeSpan.FromDays(7)));
        }

        [Fact]
        public void ItIsAcceptedOnce()
        {
            var invitation = Given();
            var user = Guid.NewGuid();

            invitation.Accept(user, Now);

            Assert.False(invitation.IsPending(Now));
            Assert.Equal(user, invitation.AcceptedUserId);
            Assert.Throws<InvalidOperationException>(() => invitation.Accept(Guid.NewGuid(), Now));
        }

        [Fact]
        public void AnExpiredInvitationCannotBeAccepted()
        {
            Assert.Throws<InvalidOperationException>(() => Given().Accept(Guid.NewGuid(), Now + TimeSpan.FromDays(8)));
        }

        [Fact]
        public void AWithdrawnInvitationCannotBeAccepted()
        {
            var invitation = Given();

            invitation.Revoke(Now);

            Assert.Throws<InvalidOperationException>(() => invitation.Accept(Guid.NewGuid(), Now));
        }

        [Fact]
        public void WithdrawingAnAcceptedInvitationChangesNothing()
        {
            // The account exists; the record of how it came to exist should not be rewritten.
            var invitation = Given();
            invitation.Accept(Guid.NewGuid(), Now);

            invitation.Revoke(Now.AddMinutes(1));

            Assert.Null(invitation.RevokedAt);
            Assert.NotNull(invitation.AcceptedAt);
        }
    }

    public sealed class Resets
    {
        private static PasswordReset Given() =>
            PasswordReset.Issue(Guid.NewGuid(), Guid.NewGuid(), "hash", Guid.NewGuid(), Now);

        [Fact]
        public void ItLastsADay()
        {
            var reset = Given();

            Assert.True(reset.IsUsable(Now + TimeSpan.FromHours(24) - TimeSpan.FromSeconds(1)));
            Assert.False(reset.IsUsable(Now + TimeSpan.FromHours(24)));
        }

        [Fact]
        public void ItIsUsedOnce()
        {
            var reset = Given();

            reset.Use(Now);

            Assert.False(reset.IsUsable(Now));
            Assert.Throws<InvalidOperationException>(() => reset.Use(Now));
        }

        [Fact]
        public void AReplacedLinkIsDead()
        {
            var reset = Given();

            reset.Revoke(Now);

            Assert.Throws<InvalidOperationException>(() => reset.Use(Now));
        }
    }
}
