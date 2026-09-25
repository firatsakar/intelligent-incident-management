using FluentValidation;
using IncidentService.Application.Abstractions;
using IncidentService.Application.Commands.UpdateIncidentStatus;
using IncidentService.Application.DTOs;
using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Enums;
using NSubstitute;

namespace IncidentService.Tests;

// The seam where the verdict rule becomes an HTTP answer: a refusal has to arrive as a 400 naming
// the field, not as an exception from inside the aggregate, and nothing may be saved or broadcast.
public sealed class UpdateIncidentStatusCommandHandlerTests
{
    private readonly IIncidentRepository _repository = Substitute.For<IIncidentRepository>();
    private readonly IRealtimeNotifier _realtime = Substitute.For<IRealtimeNotifier>();
    private readonly UpdateIncidentStatusCommandHandler _handler;
    private readonly Incident _incident = Incident.Create(
        Guid.NewGuid(),
        "checkout-service: TimeoutException",
        "Payment gateway stopped responding.",
        IncidentPriority.Medium,
        IncidentSource.Telemetry
    );

    public UpdateIncidentStatusCommandHandlerTests()
    {
        _repository.GetByIdAsync(_incident.Id, Arg.Any<CancellationToken>()).Returns(_incident);
        _handler = new UpdateIncidentStatusCommandHandler(_repository, _realtime);
    }

    private Task Handle(IncidentStatus status, IncidentVerdict? verdict) =>
        _handler.Handle(
            new UpdateIncidentStatusCommand
            {
                IncidentId = _incident.Id,
                NewStatus = status,
                Verdict = verdict,
            },
            CancellationToken.None
        );

    [Fact]
    public async Task ClosingWithoutAVerdictIsAValidationFailureOnThatField()
    {
        var refused = await Assert.ThrowsAsync<ValidationException>(
            () => Handle(IncidentStatus.Resolved, verdict: null)
        );

        Assert.Equal(nameof(UpdateIncidentStatusCommand.Verdict), Assert.Single(refused.Errors).PropertyName);
        Assert.Equal(IncidentStatus.Open, _incident.Status);

        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _realtime.DidNotReceive().IncidentChangedAsync(Arg.Any<IncidentDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClosingWithAVerdictIsSavedAndBroadcastWithIt()
    {
        await Handle(IncidentStatus.Resolved, IncidentVerdict.FalsePositive);

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _realtime.Received(1).IncidentChangedAsync(
            Arg.Is<IncidentDto>(dto =>
                dto.Status == IncidentStatus.Resolved
                && dto.Verdict == IncidentVerdict.FalsePositive
                && dto.ResolvedAt != null
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
