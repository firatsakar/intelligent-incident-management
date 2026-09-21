using BuildingBlocks.Application;

namespace BuildingBlocks.Tests;

// Masking on read protects the API. On its own it makes writes destructive, because the only
// value a client can send back for a secret is the mask — and storing that replaces the
// customer's password with "***", which surfaces as a notification that quietly did not arrive.
// These two halves have to agree or the round trip loses data.
public sealed class ConfigMaskingTests
{
    private static Dictionary<string, string> Stored() =>
        new()
        {
            ["Host"] = "smtp.example.com",
            ["Port"] = "1025",
            ["Password"] = "hunter2",
            ["ApiToken"] = "secret-token",
        };

    public sealed class Mask
    {
        [Theory]
        [InlineData("Password")]
        [InlineData("password")]
        [InlineData("ApiToken")]
        [InlineData("apikey")]
        [InlineData("ClientSecret")]
        [InlineData("AwsCredentials")]
        public void CredentialLookingKeysAreHidden(string key)
        {
            var masked = ConfigMasking.Mask(new Dictionary<string, string> { [key] = "real" });

            Assert.Equal(ConfigMasking.MaskedValue, masked[key]);
        }

        [Theory]
        [InlineData("Host")]
        [InlineData("Port")]
        [InlineData("Url")]
        [InlineData("ProjectKey")]
        public void OrdinarySettingsAreLeftAlone(string key)
        {
            var masked = ConfigMasking.Mask(new Dictionary<string, string> { [key] = "real" });

            Assert.Equal("real", masked[key]);
        }
    }

    public sealed class Restore
    {
        [Fact]
        public void AMaskedValueKeepsWhatIsStored()
        {
            // The whole point: an edit form can send back exactly what it was given without
            // destroying a secret it was never allowed to see.
            var stored = Stored();
            var submitted = ConfigMasking.Mask(stored).ToDictionary(x => x.Key, x => x.Value);

            var merged = ConfigMasking.Restore(submitted, stored);

            Assert.Equal("hunter2", merged["Password"]);
            Assert.Equal("secret-token", merged["ApiToken"]);
        }

        [Fact]
        public void ARealValueReplacesTheStoredOne()
        {
            // Rotating a credential has to still work.
            var merged = ConfigMasking.Restore(
                new Dictionary<string, string> { ["Password"] = "new-password" },
                Stored()
            );

            Assert.Equal("new-password", merged["Password"]);
        }

        [Fact]
        public void NonSecretValuesPassThroughUntouched()
        {
            var merged = ConfigMasking.Restore(
                new Dictionary<string, string> { ["Host"] = "smtp.other.com" },
                Stored()
            );

            Assert.Equal("smtp.other.com", merged["Host"]);
        }

        [Fact]
        public void AMaskForAKeyThatDoesNotExistIsNotInvented()
        {
            // There is nothing to restore, so the mask is left as the missing value it is and the
            // per-channel validation can reject it. Quietly storing "***" would be the original
            // bug wearing a different hat.
            var merged = ConfigMasking.Restore(
                new Dictionary<string, string> { ["ApiToken"] = ConfigMasking.MaskedValue },
                new Dictionary<string, string>()
            );

            Assert.Equal(ConfigMasking.MaskedValue, merged["ApiToken"]);
        }

        [Fact]
        public void AKeyTheSubmissionDropsIsDropped()
        {
            // Config is replaced wholesale by design, so removing a setting has to remain
            // possible — restore fills gaps in values, not in keys.
            var merged = ConfigMasking.Restore(
                new Dictionary<string, string> { ["Host"] = "smtp.example.com" },
                Stored()
            );

            Assert.False(merged.ContainsKey("Password"));
        }

        [Fact]
        public void TheRoundTripIsLossless()
        {
            // Read it, hand it straight back, and nothing has changed. This is the property the
            // settings screen depends on.
            var stored = Stored();

            var merged = ConfigMasking.Restore(
                ConfigMasking.Mask(stored).ToDictionary(x => x.Key, x => x.Value),
                stored
            );

            Assert.Equal(stored, merged);
        }
    }
}
