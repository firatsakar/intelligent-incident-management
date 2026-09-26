using BuildingBlocks.SharedKernel;

namespace BuildingBlocks.Tests;

// The credential machines send instead of signing in — shared by the OTLP ingest key and the
// incident API key since Adım 27.
public sealed class AccessKeyTests
{
    [Fact]
    public void AKeyCarriesItsPrefixAndIsStoredOnlyAsItsHash()
    {
        var issued = AccessKey.Generate("iim_test_");

        Assert.StartsWith("iim_test_", issued.Key);
        Assert.Equal(AccessKey.Hash(issued.Key), issued.Hash);
        Assert.Matches("^[0-9a-f]{64}$", issued.Hash);
        Assert.DoesNotContain(issued.Key, issued.Hash);
    }

    [Fact]
    public void TheDisplayPrefixTellsKeysApartWithoutBeingUsable()
    {
        var issued = AccessKey.Generate("iim_test_");

        Assert.Equal("iim_test_".Length + 6, issued.DisplayPrefix.Length);
        Assert.StartsWith(issued.DisplayPrefix, issued.Key);
        Assert.True(issued.Key.Length > issued.DisplayPrefix.Length + 32);
    }

    [Fact]
    public void EveryKeyIsNewAndUrlSafe()
    {
        var keys = Enumerable.Range(0, 50).Select(_ => AccessKey.Generate("p_").Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.All(keys, key => Assert.Matches("^p_[A-Za-z0-9_-]+$", key));
    }
}
