using System.Diagnostics;
using BuildingBlocks.Outbox;
using BuildingBlocks.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Tests;

// The row is where the dispatcher learns, minutes later, whose work a message is and which trace it
// belongs to. Both are stamped here, inside the SaveChanges that changed the aggregate — the last
// moment either is known.
public sealed class OutboxInterceptorTests
{
    private readonly OrganizationContext _organization = new();

    public OutboxInterceptorTests()
    {
        _organization.Set(Guid.NewGuid());
    }

    [Fact]
    public async Task TheRowCarriesTheTraceItWasWrittenInside()
    {
        using var listener = ListenToEverything();
        using var request = new ActivitySource("test").StartActivity("request");

        var row = await SaveOneEventAsync();

        Assert.NotNull(request);
        Assert.Equal(request.Id, row.TraceParent);
        Assert.Equal(_organization.OrganizationId, row.OrganizationId);
    }

    [Fact]
    public async Task ARowWrittenWithNothingInFlightCarriesNoTrace()
    {
        var row = await SaveOneEventAsync();

        Assert.Null(row.TraceParent);
    }

    private async Task<OutboxMessage> SaveOneEventAsync()
    {
        var options = new DbContextOptionsBuilder<TestContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new ConvertDomainEventsToOutboxInterceptor(_organization))
            .Options;

        await using var context = new TestContext(options);

        context.Add(TestAggregate.Raise());
        await context.SaveChangesAsync();

        return await context.Set<OutboxMessage>().SingleAsync();
    }

    private static ActivityListener ListenToEverything()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "test",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(listener);

        return listener;
    }

    private sealed class TestContext(DbContextOptions<TestContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestAggregate>().Ignore(x => x.DomainEvents);
            modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        }
    }

    private sealed class TestAggregate : AggregateRoot
    {
        public static TestAggregate Raise()
        {
            var aggregate = new TestAggregate();
            aggregate.AddDomainEvent(new Happened());

            return aggregate;
        }
    }

    private sealed record Happened : DomainEvent;
}
