using BuildingBlocks.Outbox;

namespace BuildingBlocks.Tests;

// Adım 28: a row that fails waits longer each time and, after hours of failing, stops. Before this
// it was retried every five seconds forever, and a failure that would never go away republished
// its event every five seconds for as long as nobody looked.
public sealed class OutboxRetryPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    private static OutboxMessage Row() =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = "IncidentAnalysisCompletedDomainEvent",
            Payload = "{}",
            OccurredOn = Now,
            OrganizationId = Guid.NewGuid(),
        };

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(8, 640)]
    [InlineData(9, 900)]
    [InlineData(19, 900)]
    public void TheWaitDoublesFromFiveSecondsAndNeverExceedsFifteenMinutes(int failures, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), OutboxMessage.RetryDelay(failures));

    [Fact]
    public void AFailureSchedulesTheNextAttemptInsteadOfTheNextTick()
    {
        var row = Row();

        var parked = row.MarkFailed("broker unreachable", Now);

        Assert.False(parked);
        Assert.Equal(1, row.RetryCount);
        Assert.Equal("broker unreachable", row.Error);
        Assert.Equal(Now.AddSeconds(5), row.NextAttemptAt);
        Assert.Null(row.ParkedAt);
    }

    [Fact]
    public void TheTwentiethFailureParksTheRowAfterAboutThreeHours()
    {
        var row = Row();
        var at = Now;
        var parkedOn = 0;

        for (var attempt = 1; attempt <= OutboxMessage.MaxAttempts; attempt++)
        {
            if (row.MarkFailed("handler bug", at))
                parkedOn = attempt;
            else
                at = row.NextAttemptAt!.Value;
        }

        Assert.Equal(OutboxMessage.MaxAttempts, parkedOn);
        Assert.Equal(at, row.ParkedAt);
        Assert.Null(row.NextAttemptAt);
        Assert.InRange(at - Now, TimeSpan.FromHours(3), TimeSpan.FromHours(3.5));
    }

    [Fact]
    public void SuccessClearsWhatTheFailuresLeft()
    {
        var row = Row();
        row.MarkFailed("broker unreachable", Now);

        row.MarkDispatched(Now.AddSeconds(5));

        Assert.Equal(Now.AddSeconds(5), row.ProcessedOn);
        Assert.Null(row.Error);
        Assert.Null(row.NextAttemptAt);
    }

    [Fact]
    public void OnlyRowsThatAreNeitherDoneNorParkedNorWaitingAreDue()
    {
        var fresh = Row();

        var waiting = Row();
        waiting.MarkFailed("x", Now);

        var ready = Row();
        ready.MarkFailed("x", Now.AddMinutes(-1));

        var done = Row();
        done.MarkDispatched(Now);

        var parked = Row();
        parked.ParkedAt = Now;

        var due = new[] { fresh, waiting, ready, done, parked }.AsQueryable().Where(OutboxMessage.Due(Now)).ToList();

        Assert.Equal([fresh, ready], due);
    }
}
