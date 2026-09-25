using BuildingBlocks.SharedKernel;
using FluentValidation;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Accounts;
using IdentityService.Application.DTOs;
using IdentityService.Application.Sessions;
using IdentityService.Application.Setup;
using IdentityService.Domain.Aggregates;
using IdentityService.Domain.Enums;
using IdentityService.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IdentityService.Application.Commands.CompleteSetup;

/// <summary>
/// The first-run setup (Adım 25): an empty installation gets its organisation and its first Admin,
/// and the Admin is signed in. Runs once — after it, nothing opens it again.
/// </summary>
public sealed record CompleteSetupCommand(
    string SetupCode,
    string OrganizationName,
    string DisplayName,
    string Email,
    string Password
) : IRequest<IssuedSession>;

public sealed class CompleteSetupCommandValidator : AbstractValidator<CompleteSetupCommand>
{
    public CompleteSetupCommandValidator()
    {
        RuleFor(x => x.SetupCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.OrganizationName).Must(name => !string.IsNullOrWhiteSpace(name)).MaximumLength(128);
        RuleFor(x => x.DisplayName).Must(name => !string.IsNullOrWhiteSpace(name)).MaximumLength(128);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
        RuleFor(x => x.Password).NewPassword();
    }
}

public sealed class CompleteSetupCommandHandler : IRequestHandler<CompleteSetupCommand, IssuedSession>
{
    private readonly IUserRepository _users;
    private readonly IOrganizationRepository _organizations;
    private readonly IPasswordHasher _hasher;
    private readonly SessionIssuer _issuer;
    private readonly SetupCode _setup;
    private readonly IOrganizationContext _organization;
    private readonly ILogger<CompleteSetupCommandHandler> _logger;

    public CompleteSetupCommandHandler(
        IUserRepository users,
        IOrganizationRepository organizations,
        IPasswordHasher hasher,
        SessionIssuer issuer,
        SetupCode setup,
        IOrganizationContext organization,
        ILogger<CompleteSetupCommandHandler> logger
    )
    {
        _users = users;
        _organizations = organizations;
        _hasher = hasher;
        _issuer = issuer;
        _setup = setup;
        _organization = organization;
        _logger = logger;
    }

    public async Task<IssuedSession> Handle(CompleteSetupCommand request, CancellationToken cancellationToken)
    {
        await _setup.Gate.WaitAsync(cancellationToken);

        try
        {
            // One answer for a wrong code, an installation that already has users, and a setup
            // that already happened: each would otherwise tell a caller something about the server.
            // Checked under the gate, so two requests with the right code cannot both find the
            // database empty.
            if (!_setup.Matches(request.SetupCode) || await _users.AnyAsync(cancellationToken))
                throw new SetupNotAvailableException();

            var organization = Organization.Create(request.OrganizationName);
            await _organizations.AddAsync(organization, cancellationToken);

            // The outbox stamps every row with the scope in flight, and an anonymous request has
            // none — the organisation it is about is the one created on the line above. The same
            // step IdentitySeeder takes; OrganizationCreatedEvent then gives the new organisation
            // its default detection rule in the telemetry service.
            _organization.Set(organization.Id);

            var user = User.Create(
                organization.Id,
                request.Email,
                request.DisplayName.Trim(),
                _hasher.Hash(request.Password),
                UserRole.Admin
            );

            await _users.AddAsync(user, cancellationToken);

            // Saved before the session is issued: the session reads the organisation's name back
            // from the database to describe who signed in.
            await _users.SaveChangesAsync(cancellationToken);

            // Spent at the moment the installation stops being empty, not after the session: a
            // failure past this point leaves an Admin who can sign in, never a reusable code.
            _setup.Consume();

            var session = await _issuer.IssueAsync(user, cancellationToken);
            await _users.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "First-run setup completed: organisation {Organization} with administrator {Email}.",
                organization.Name,
                user.Email
            );

            return session;
        }
        finally
        {
            _setup.Gate.Release();
        }
    }
}
