using BuildingBlocks.SharedKernel;

namespace BuildingBlocks.Tests;

// The ambient answer to "whose data is this". Everything about it is about refusing to guess.
public sealed class OrganizationContextTests
{
    [Fact]
    public void StartsWithoutOne()
    {
        Assert.Null(new OrganizationContext().OrganizationId);
    }

    [Fact]
    public void RemembersWhatWasSet()
    {
        var id = Guid.NewGuid();
        var context = new OrganizationContext();

        context.Set(id);

        Assert.Equal(id, context.OrganizationId);
        Assert.Equal(id, context.Required);
    }

    // The difference between the two properties is the whole point. A query filter asking for
    // Required must not silently read everybody's rows because nobody established a scope.
    [Fact]
    public void RequiredRefusesRatherThanReturningNothing()
    {
        Assert.Throws<InvalidOperationException>(() => new OrganizationContext().Required);
    }

    // An empty guid is what an unparsed claim, a default struct or a forgotten assignment all
    // look like. Accepting it would mean a scope that matches rows nobody owns.
    [Fact]
    public void AnEmptyIdIsNotAScope()
    {
        Assert.Throws<ArgumentException>(() => new OrganizationContext().Set(Guid.Empty));
    }
}
