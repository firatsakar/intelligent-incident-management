using BuildingBlocks.SharedKernel;
using FluentValidation;
using IncidentService.Application.Abstractions;
using IncidentService.Application.Commands.CreateIncident;
using IncidentService.Application.Commands.CreateIncidentApiKey;
using IncidentService.Application.Commands.IntakeIncident;
using IncidentService.Application.DTOs;
using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace IncidentService.Tests;

// Adım 27: external systems open incidents with an organisation's API key. The key can do one
// thing; an alert that is resent while its incident is open finds that incident instead of
// opening another.
public sealed class IncidentApiTests
{
    private static readonly Guid Organization = Guid.NewGuid();

    public sealed class Keys
    {
        [Fact]
        public void AKeyIsIssuedOnceAndKeptOnlyAsItsHash()
        {
            var (key, secret) = IncidentApiKey.Issue(Organization, "  Grafana  ", "Ayşe Yılmaz");

            Assert.StartsWith(IncidentApiKey.Prefix, secret);
            Assert.Equal(AccessKey.Hash(secret), key.KeyHash);
            Assert.StartsWith(key.KeyPrefix, secret);
            Assert.Equal("Grafana", key.Name);
            Assert.Equal("Ayşe Yılmaz", key.CreatedBy);
            Assert.Equal(Organization, key.OrganizationId);
            Assert.Null(key.LastUsedAt);
        }

        [Fact]
        public void UseIsRecordedToTheMinuteNotOnEveryRequest()
        {
            var (key, _) = IncidentApiKey.Issue(Organization, "CI", "Admin");
            var now = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

            Assert.True(key.MarkUsed(now));
            Assert.False(key.MarkUsed(now.AddSeconds(30)));
            Assert.Equal(now, key.LastUsedAt);
            Assert.True(key.MarkUsed(now.AddMinutes(1)));
            Assert.Equal(now.AddMinutes(1), key.LastUsedAt);
        }

        private readonly IIncidentApiKeyRepository _keys = Substitute.For<IIncidentApiKeyRepository>();
        private readonly OrganizationContext _organization = new();

        private CreateIncidentApiKeyCommandHandler Handler()
        {
            _organization.Set(Organization);

            return new CreateIncidentApiKeyCommandHandler(
                _keys,
                _organization,
                NullLogger<CreateIncidentApiKeyCommandHandler>.Instance
            );
        }

        [Fact]
        public async Task CreatingReturnsTheValueOnceAndStoresOnlyTheHash()
        {
            IncidentApiKey? stored = null;
            await _keys.AddAsync(Arg.Do<IncidentApiKey>(key => stored = key), Arg.Any<CancellationToken>());

            var issued = await Handler().Handle(new CreateIncidentApiKeyCommand("Grafana", "Ayşe"), CancellationToken.None);

            Assert.NotNull(stored);
            Assert.Equal(AccessKey.Hash(issued.Key), stored.KeyHash);
            Assert.Equal(stored.Id, issued.Id);
            Assert.Equal("Grafana", issued.Name);
            await _keys.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ASecondKeyWithTheSameNameIsRefusedAsAFieldError()
        {
            _keys.NameExistsAsync("Grafana", Arg.Any<CancellationToken>()).Returns(true);

            var error = await Assert.ThrowsAsync<ValidationException>(
                () => Handler().Handle(new CreateIncidentApiKeyCommand(" Grafana ", "Ayşe"), CancellationToken.None)
            );

            Assert.Contains(error.Errors, failure => failure.PropertyName == "Name");
            await _keys.DidNotReceive().AddAsync(Arg.Any<IncidentApiKey>(), Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void AKeyNeedsAName(string name) =>
            Assert.False(new CreateIncidentApiKeyCommandValidator().Validate(new CreateIncidentApiKeyCommand(name, "Ayşe")).IsValid);
    }

    public sealed class Intake
    {
        private readonly IIncidentRepository _incidents = Substitute.For<IIncidentRepository>();
        private readonly ISender _sender = Substitute.For<ISender>();
        private CreateIncidentCommand? _sent;

        public Intake()
        {
            _sender
                .Send(Arg.Any<CreateIncidentCommand>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    _sent = (CreateIncidentCommand)call[0];

                    return IncidentDto.FromDomain(
                        Incident.Create(Organization, _sent.Title, _sent.Description, _sent.Priority, _sent.Source)
                    );
                });
        }

        private IntakeIncidentCommandHandler Handler() =>
            new(_incidents, _sender, NullLogger<IntakeIncidentCommandHandler>.Instance);

        private static IntakeIncidentCommand Request(string? externalId = "alert-42", IncidentPriority? priority = null) =>
            new()
            {
                Title = "  Checkout error rate above 5%  ",
                Description = "Fired by Grafana.",
                Priority = priority,
                ExternalId = externalId,
                KeyName = "Grafana",
            };

        private static Incident Open(string externalId) =>
            Incident.Create(Organization, "Earlier", "Earlier", IncidentPriority.High, IncidentSource.Alert, externalId: externalId, reportedBy: "Grafana");

        [Fact]
        public async Task ANewProblemOpensAnAlertIncidentInTheKeysName()
        {
            var result = await Handler().Handle(Request(), CancellationToken.None);

            Assert.True(result.Created);
            Assert.NotNull(_sent);
            Assert.Equal(IncidentSource.Alert, _sent.Source);
            Assert.Equal("alert-42", _sent.ExternalId);
            Assert.Equal("Grafana", _sent.ReportedBy);
            Assert.Equal("Checkout error rate above 5%", _sent.Title);
        }

        [Fact]
        public async Task WithoutAPriorityItIsMediumAndTheAnalysisSuggestsOne()
        {
            await Handler().Handle(Request(priority: null), CancellationToken.None);

            Assert.Equal(IncidentPriority.Medium, _sent!.Priority);
        }

        [Fact]
        public async Task TheSameExternalIdWhileItsIncidentIsOpenFindsThatIncident()
        {
            var open = Open("alert-42");
            _incidents.GetOpenByExternalIdAsync("alert-42", Arg.Any<CancellationToken>()).Returns(open);

            var result = await Handler().Handle(Request(), CancellationToken.None);

            Assert.False(result.Created);
            Assert.Equal(open.Id, result.Id);
            Assert.Null(_sent);
        }

        [Fact]
        public async Task WithoutAnExternalIdEveryRequestIsANewIncident()
        {
            var result = await Handler().Handle(Request(externalId: "  "), CancellationToken.None);

            Assert.True(result.Created);
            Assert.Null(_sent!.ExternalId);
            await _incidents.DidNotReceive().GetOpenByExternalIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task LosingTheRaceForAnExternalIdAnswersWithTheWinnersIncident()
        {
            var winner = Open("alert-42");

            // Nothing open when this request looks; the other request's incident is saved first.
            _incidents
                .GetOpenByExternalIdAsync("alert-42", Arg.Any<CancellationToken>())
                .Returns(_ => null, _ => winner);
            _sender
                .Send(Arg.Any<CreateIncidentCommand>(), Arg.Any<CancellationToken>())
                .Throws(new DuplicateExternalIdException(new Exception("23505")));

            var result = await Handler().Handle(Request(), CancellationToken.None);

            Assert.False(result.Created);
            Assert.Equal(winner.Id, result.Id);
        }

        [Fact]
        public void TheBodyIsCheckedInTheSendersFieldNames()
        {
            var validator = new IntakeIncidentCommandValidator();

            Assert.True(validator.Validate(Request(priority: null)).IsValid);
            Assert.False(validator.Validate(Request() with { Title = " " }).IsValid);
            Assert.False(validator.Validate(Request() with { Description = "" }).IsValid);
            Assert.False(validator.Validate(Request(externalId: new string('x', 201))).IsValid);
            Assert.False(validator.Validate(Request(priority: (IncidentPriority)42)).IsValid);
        }
    }
}
