using BuildingBlocks.SharedKernel;
using FluentValidation;
using NSubstitute;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.Commands.CreateTelemetrySource;
using TelemetryIngestionService.Application.Commands.RotateIngestKey;
using TelemetryIngestionService.Application.DTOs;
using TelemetryIngestionService.Application.Validators;
using TelemetryIngestionService.Domain.Aggregates;
using TelemetryIngestionService.Domain.Enums;
using TelemetryIngestionService.Domain.Services;

namespace TelemetryIngestionService.Tests;

// A pushed source is configured on the sender's side and identified by a key. What matters: the
// key is what decides the organisation, it is shown exactly once, and it never reaches anyone the
// response was not for.
public sealed class PushedSourceTests
{
    private static readonly Dictionary<string, string> NoConfig = [];

    public sealed class Keys
    {
        [Fact]
        public void AKeyIsRecognisableAndItsHashIsWhatIsStored()
        {
            var issued = IngestKey.Generate();

            Assert.StartsWith(IngestKey.Prefix, issued.Key);
            Assert.Equal(IngestKey.Hash(issued.Key), issued.Hash);
            Assert.DoesNotContain(issued.Key, issued.Hash);
            Assert.StartsWith(issued.DisplayPrefix, issued.Key);
            Assert.True(issued.DisplayPrefix.Length < issued.Key.Length / 3);
        }

        [Fact]
        public void TwoKeysAreNeverTheSame()
        {
            Assert.NotEqual(IngestKey.Generate().Key, IngestKey.Generate().Key);
        }

        [Fact]
        public void APolledSourceCannotBeIssuedAKey()
        {
            // A Seq source with a key would be a key that authenticates nothing — or, worse,
            // one a future endpoint accepts for a source that was never meant to be pushed to.
            var seq = TelemetrySource.Create(Guid.NewGuid(), "seq", TelemetrySourceKind.Seq, new Dictionary<string, string> { ["Url"] = "http://seq" });

            Assert.Throws<InvalidOperationException>(() => seq.IssueIngestKey("hash", "prefix"));
        }
    }

    public sealed class Configuration
    {
        [Fact]
        public void APushedSourceNeedsNoSettings()
        {
            var result = new CreateTelemetrySourceCommandValidator().Validate(
                new CreateTelemetrySourceCommand { Name = "collector", Kind = TelemetrySourceKind.Otlp, Config = NoConfig }
            );

            Assert.True(result.IsValid);
        }

        [Fact]
        public void APolledSourceStillDoes()
        {
            var result = new CreateTelemetrySourceCommandValidator().Validate(
                new CreateTelemetrySourceCommand { Name = "seq", Kind = TelemetrySourceKind.Seq, Config = NoConfig }
            );

            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("Warning", true)]
        [InlineData("error", true)]
        [InlineData("Information", false)]
        [InlineData("Fatal", false)]
        [InlineData("loud", false)]
        public void MinimumSeverityIsWarningOrError(string value, bool valid)
        {
            // Information would let a collector with no filter fill the table; Fatal would drop
            // every burst that is not a crash.
            var result = new CreateTelemetrySourceCommandValidator().Validate(
                new CreateTelemetrySourceCommand
                {
                    Name = "collector",
                    Kind = TelemetrySourceKind.Otlp,
                    Config = new Dictionary<string, string> { [TelemetrySourceConfigRules.MinimumSeverityKey] = value },
                }
            );

            Assert.Equal(valid, result.IsValid);
        }

        [Fact]
        public void UnsetMinimumSeverityMeansError()
        {
            Assert.Equal(LogSeverity.Error, TelemetrySourceConfigRules.MinimumSeverity(NoConfig));
        }
    }

    public sealed class Issuing
    {
        private readonly ITelemetrySourceRepository _sources = Substitute.For<ITelemetrySourceRepository>();
        private readonly IRealtimeNotifier _realtime = Substitute.For<IRealtimeNotifier>();
        private readonly OrganizationContext _organization = new();
        private readonly List<TelemetrySourceDto> _broadcast = [];

        public Issuing()
        {
            _organization.Set(Guid.NewGuid());

            _realtime
                .When(x => x.SourceChangedAsync(Arg.Any<TelemetrySourceDto>(), Arg.Any<CancellationToken>()))
                .Do(call => _broadcast.Add(call.Arg<TelemetrySourceDto>()));
        }

        [Fact]
        public async Task CreatingAPushedSourceReturnsItsKeyOnceAndBroadcastsItWithout()
        {
            TelemetrySource? stored = null;
            await _sources.AddAsync(Arg.Do<TelemetrySource>(source => stored = source), Arg.Any<CancellationToken>());

            var dto = await new CreateTelemetrySourceCommandHandler(_sources, _realtime, _organization).Handle(
                new CreateTelemetrySourceCommand { Name = "collector", Kind = TelemetrySourceKind.Otlp, Config = NoConfig },
                CancellationToken.None
            );

            Assert.NotNull(dto.IngestKey);
            Assert.NotNull(stored);
            Assert.Equal(IngestKey.Hash(dto.IngestKey), stored.IngestKeyHash);
            Assert.Equal(stored.IngestKeyPrefix, dto.IngestKeyPrefix);

            // Every other console of the organisation hears about the new source — not its key.
            Assert.Null(Assert.Single(_broadcast).IngestKey);
        }

        [Fact]
        public async Task CreatingAPolledSourceIssuesNoKey()
        {
            var dto = await new CreateTelemetrySourceCommandHandler(_sources, _realtime, _organization).Handle(
                new CreateTelemetrySourceCommand
                {
                    Name = "seq",
                    Kind = TelemetrySourceKind.Seq,
                    Config = new Dictionary<string, string> { ["Url"] = "http://seq" },
                },
                CancellationToken.None
            );

            Assert.Null(dto.IngestKey);
            Assert.Null(dto.IngestKeyPrefix);
        }

        [Fact]
        public async Task RotatingReplacesTheKey()
        {
            var source = TelemetrySource.Create(_organization.Required, "collector", TelemetrySourceKind.Otlp, NoConfig);
            var first = IngestKey.Generate();
            source.IssueIngestKey(first.Hash, first.DisplayPrefix);
            _sources.GetByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

            var dto = await new RotateIngestKeyCommandHandler(_sources, _realtime).Handle(
                new RotateIngestKeyCommand(source.Id),
                CancellationToken.None
            );

            Assert.NotNull(dto.IngestKey);
            Assert.NotEqual(first.Hash, source.IngestKeyHash);
            Assert.Equal(IngestKey.Hash(dto.IngestKey), source.IngestKeyHash);
            Assert.Null(Assert.Single(_broadcast).IngestKey);
        }

        [Fact]
        public async Task APolledSourceHasNothingToRotate()
        {
            var seq = TelemetrySource.Create(_organization.Required, "seq", TelemetrySourceKind.Seq, new Dictionary<string, string> { ["Url"] = "http://seq" });
            _sources.GetByIdAsync(seq.Id, Arg.Any<CancellationToken>()).Returns(seq);

            await Assert.ThrowsAsync<ValidationException>(() =>
                new RotateIngestKeyCommandHandler(_sources, _realtime).Handle(
                    new RotateIngestKeyCommand(seq.Id),
                    CancellationToken.None
                )
            );
        }
    }
}
