using AgentOrchestrator.Domain.ValueObjects;

namespace AgentOrchestrator.Tests;

// EF materialises the related changes by adding to the collection the initializer created. A
// fixed-size default (an array, which is what `[]` makes for an IReadOnlyList) let a result with a
// change in it be saved but not read back, and the outbox re-published it every five seconds.
public sealed class AnalysisResultTests
{
    [Fact]
    public void RelatedChangesStartAsACollectionThatCanBeAddedTo()
    {
        var result = new AnalysisResult
        {
            SuggestedPriority = "High",
            SuggestedCategory = "Application",
            Reasoning = "r",
        };

        var collection = Assert.IsAssignableFrom<ICollection<RelatedChange>>(result.RelatedChanges);

        Assert.False(collection.IsReadOnly);
    }
}
