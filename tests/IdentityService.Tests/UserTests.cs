using System.Globalization;
using IdentityService.Domain.Aggregates;
using IdentityService.Domain.Enums;

namespace IdentityService.Tests;

// The address is the account. Everything here is about there being exactly one spelling of it.
public sealed class UserTests
{
    private static User Given(string email = "ops@example.com", UserRole role = UserRole.Engineer) =>
        User.Create(Guid.NewGuid(), email, "Ops", "hash", role);

    public sealed class Normalization
    {
        [Theory]
        [InlineData("ops@example.com", "ops@example.com")]
        [InlineData("  ops@example.com  ", "ops@example.com")]
        [InlineData("OPS@Example.COM", "ops@example.com")]
        public void OneAddressHasOneSpelling(string typed, string stored)
        {
            Assert.Equal(stored, User.Normalize(typed));
        }

        // The reason Normalize uses ToLowerInvariant and not ToLower. Under a Turkish culture the
        // culture-sensitive lowercase maps I to ı, so this same call would return "fırat@…" and
        // the person would fail to sign in depending on whether they typed their own name in
        // capitals. The machines this team develops on run that culture, so the bug would be
        // local, intermittent, and blamed on the password.
        [Fact]
        public void TurkishCultureDoesNotTurnAnIIntoADotlessOne()
        {
            var original = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

                Assert.Equal("firat@example.com", User.Normalize("FIRAT@EXAMPLE.COM"));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [Fact]
        public void CreateStoresTheNormalisedForm()
        {
            var user = Given("  OPS@Example.COM ");

            Assert.Equal("ops@example.com", user.Email);
        }
    }

    public sealed class Lifecycle
    {
        [Fact]
        public void ANewUserCanSignIn()
        {
            Assert.True(Given().IsActive);
        }

        // Deactivating rather than deleting: the rows that name this person outlive their access.
        [Fact]
        public void DeactivatingLeavesTheAccountInPlace()
        {
            var user = Given();
            var id = user.Id;

            user.Deactivate();

            Assert.False(user.IsActive);
            Assert.Equal(id, user.Id);
            Assert.Equal("ops@example.com", user.Email);
        }

        [Fact]
        public void ReactivatingRestoresAccess()
        {
            var user = Given();

            user.Deactivate();
            user.Activate();

            Assert.True(user.IsActive);
        }

        [Fact]
        public void ChangingThePasswordReplacesOnlyTheHash()
        {
            var user = Given();

            user.ChangePassword("a different hash");

            Assert.Equal("a different hash", user.PasswordHash);
            Assert.NotNull(user.UpdatedAt);
        }
    }
}
