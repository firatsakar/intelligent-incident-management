using BuildingBlocks.SharedKernel;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.Aggregates;
using IdentityService.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IdentityService.Infrastructure.Persistence;

/// <summary>
/// Puts the first organisation and the first administrator into an empty database, from
/// configuration.
/// </summary>
/// <remarks>
/// <para>
/// There is no default credential anywhere in this repository, and this class is where that
/// decision is enforced rather than merely intended. With nothing configured it creates nothing
/// and says so — a service that no one can sign in to is a visible problem, where a service with a
/// well-known password is an invisible one.
/// </para>
/// <para>
/// The guard is "any user at all", not "this user": once someone exists, the account they use is
/// theirs to change, and a seeder that reinstated a password on every restart would quietly undo
/// that.
/// </para>
/// </remarks>
public sealed class IdentitySeeder : IHostedService
{
    /// <summary>Configuration section holding the first administrator. Belongs in user secrets.</summary>
    public const string SeedConfigurationSection = "Identity:Seed";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<IdentitySeeder> logger
    )
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();

            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            if (await users.AnyAsync(cancellationToken))
                return;

            var section = _configuration.GetSection(SeedConfigurationSection);

            var email = section["Email"];
            var password = section["Password"];

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning(
                    "No users exist and no seed administrator is configured, so nobody can sign in. "
                        + "Set {Section}:Email and {Section}:Password (user secrets in development) and restart.",
                    SeedConfigurationSection,
                    SeedConfigurationSection
                );

                return;
            }

            var organizations = scope.ServiceProvider.GetRequiredService<IOrganizationRepository>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            var organization = Organization.Create(
                section["OrganizationName"] ?? "Acme Operations"
            );

            await organizations.AddAsync(organization, cancellationToken);

            // The outbox interceptor stamps every row it writes with the scope in flight, and
            // there is no scope here to inherit: this is a hosted service started by the host, and
            // the organisation it is about is the one being created on the line above. Setting it
            // explicitly is what keeps the interceptor's rule absolute — every outbox row has an
            // owner, and nothing falls back to a default.
            scope.ServiceProvider.GetRequiredService<IOrganizationContext>().Set(organization.Id);

            var user = User.Create(
                organization.Id,
                email,
                section["DisplayName"] ?? email,
                hasher.Hash(password),
                UserRole.Admin
            );

            await users.AddAsync(user, cancellationToken);

            // One SaveChanges for both, through either repository: they share the DbContext, and
            // an organisation without its first administrator is not a state worth reaching.
            // It is also what puts OrganizationCreatedDomainEvent into the outbox in the same
            // transaction as the row it is about.
            await users.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Seeded organisation {Organization} with administrator {Email}.",
                organization.Name,
                user.Email
            );
        }
        catch (Exception ex)
        {
            // Same stance as DetectionRuleSeeder: a seeding failure is logged, not fatal. The
            // service is still useful to anyone who already has an account.
            _logger.LogError(ex, "Identity seeding failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
