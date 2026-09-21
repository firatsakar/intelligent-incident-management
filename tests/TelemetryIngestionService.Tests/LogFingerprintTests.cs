using TelemetryIngestionService.Domain.Services;

namespace TelemetryIngestionService.Tests;

// Turns a log event into the identity of the error behind it. Everything downstream — dedup,
// counters, precedent — is keyed on the result, so a fingerprint that is too eager collapses
// unrelated failures into one and a fingerprint that is too shy raises an incident per request.
public sealed class LogFingerprintTests
{
    public sealed class Normalize
    {
        [Fact]
        public void MessageTemplate_WinsOverTheRenderedMessage()
        {
            // A template is already normalised by construction: its volatile parts are named
            // placeholders the author chose. No amount of guessing beats that.
            var result = LogFingerprint.Normalize(
                "Payment for order 4471 failed after 3 retries",
                "Payment for order {OrderId} failed after {RetryCount} retries"
            );

            Assert.Equal("Payment for order {OrderId} failed after {RetryCount} retries", result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void MissingTemplate_FallsBackToTheRegexPath(string? template)
        {
            // The arm that has never run against real data: every event in the demo store carries
            // a Serilog template, so the template path covered all of them.
            var result = LogFingerprint.Normalize("Order 4471 failed", template);

            Assert.Equal("Order # failed", result);
        }

        [Fact]
        public void TwoRendersOfTheSameError_NormaliseToTheSameText()
        {
            // The whole point of the fallback: different values, one identity.
            var first = LogFingerprint.Normalize("Order 4471 failed after 3 retries", null);
            var second = LogFingerprint.Normalize("Order 9982 failed after 11 retries", null);

            Assert.Equal(first, second);
        }

        [Fact]
        public void GuidsAreMasked()
        {
            var result = LogFingerprint.Normalize(
                "Tenant 3f2504e0-4f89-11d3-9a0c-0305e82c3301 not found",
                null
            );

            Assert.Equal("Tenant <id> not found", result);
        }

        [Fact]
        public void GuidsWinOverHexAndNumbers()
        {
            // Order is load-bearing: a GUID is also a run of hex, and hex is also a run of
            // digits. If the number pattern ran first the GUID would come out as "#-#-#-#-#".
            var result = LogFingerprint.Normalize(
                "id=3f2504e0-4f89-11d3-9a0c-0305e82c3301",
                null
            );

            Assert.DoesNotContain("#", result);
            Assert.Contains("<id>", result);
        }

        [Fact]
        public void EmailsAreMasked()
        {
            var result = LogFingerprint.Normalize("Login failed for a.user+tag@example.co.uk", null);

            Assert.Equal("Login failed for <email>", result);
        }

        [Fact]
        public void LongHexRunsAreMasked()
        {
            var result = LogFingerprint.Normalize("trace deadbeefdeadbeef aborted", null);

            Assert.Equal("trace <hex> aborted", result);
        }

        [Fact]
        public void ShortHexRunsFallThroughToTheNumberPattern()
        {
            // Below sixteen characters it is not treated as an identifier. "beef" has no digits
            // to mask and survives; "1234" is a number.
            var result = LogFingerprint.Normalize("trace beef 1234 aborted", null);

            Assert.Equal("trace beef # aborted", result);
        }

        [Fact]
        public void ALongRunOfDigitsIsTreatedAsHexRatherThanANumber()
        {
            // Documents a consequence of the ordering rather than an intention: digits are valid
            // hex characters, so a sixteen-digit number matches the hex pattern first. Harmless
            // for identity — both masks normalise consistently — but surprising when reading a
            // normalised message back.
            var result = LogFingerprint.Normalize("card 4111111111111111 declined", null);

            Assert.Equal("card <hex> declined", result);
        }

        [Fact]
        public void DecimalsAreMaskedAsOneNumber()
        {
            var result = LogFingerprint.Normalize("Request took 1284.57ms", null);

            Assert.Equal("Request took #ms", result);
        }

        [Fact]
        public void LongMessagesAreTruncated()
        {
            var result = LogFingerprint.Normalize(new string('x', 5000), null);

            Assert.Equal(4096, result.Length);
        }

        [Fact]
        public void LongTemplatesAreTruncatedToo()
        {
            var result = LogFingerprint.Normalize("ignored", new string('y', 5000));

            Assert.Equal(4096, result.Length);
        }
    }

    public sealed class Compute
    {
        private const string Message = "Payment for order {OrderId} failed";

        [Fact]
        public void IsDeterministic()
        {
            Assert.Equal(
                LogFingerprint.Compute("checkout-service", "TimeoutException", Message),
                LogFingerprint.Compute("checkout-service", "TimeoutException", Message)
            );
        }

        [Fact]
        public void IsThirtyTwoLowercaseHexCharacters()
        {
            var fingerprint = LogFingerprint.Compute("checkout-service", null, Message);

            // Half of SHA-256, which is what the column is sized for.
            Assert.Equal(32, fingerprint.Length);
            Assert.Matches("^[0-9a-f]{32}$", fingerprint);
        }

        [Fact]
        public void DiffersByService()
        {
            Assert.NotEqual(
                LogFingerprint.Compute("checkout-service", "TimeoutException", Message),
                LogFingerprint.Compute("billing-service", "TimeoutException", Message)
            );
        }

        [Fact]
        public void DiffersByExceptionType()
        {
            Assert.NotEqual(
                LogFingerprint.Compute("checkout-service", "TimeoutException", Message),
                LogFingerprint.Compute("checkout-service", "SocketException", Message)
            );
        }

        [Fact]
        public void DiffersByMessage()
        {
            Assert.NotEqual(
                LogFingerprint.Compute("checkout-service", "TimeoutException", Message),
                LogFingerprint.Compute("checkout-service", "TimeoutException", "Something else")
            );
        }

        [Fact]
        public void TreatsANullExceptionTypeAsAnEmptyOne()
        {
            Assert.Equal(
                LogFingerprint.Compute("checkout-service", null, Message),
                LogFingerprint.Compute("checkout-service", string.Empty, Message)
            );
        }

        [Fact]
        public void ShiftingAFieldBoundaryDoesNotCollide()
        {
            // The separator earning its keep. Without one, "checkout" + "Service" + "Timeout" and
            // "checkoutService" + "" + "Timeout" would hash the same material, and two unrelated
            // errors would share one signature, one counter and one incident.
            //
            // The separator is U+001F, which is invisible in most editors and diffs — so it is
            // exactly the kind of character a reformat or a copy-paste deletes without anybody
            // noticing. This test is the thing that would notice.
            Assert.NotEqual(
                LogFingerprint.Compute("checkoutService", null, "Timeout"),
                LogFingerprint.Compute("checkout", "Service", "Timeout")
            );
        }

        [Fact]
        public void AMessageThatAbsorbsTheExceptionTypeDoesNotCollideEither()
        {
            // The other direction across the second boundary.
            Assert.NotEqual(
                LogFingerprint.Compute("checkout-service", "Timeout", "Exception thrown"),
                LogFingerprint.Compute("checkout-service", null, "TimeoutException thrown")
            );
        }
    }
}
