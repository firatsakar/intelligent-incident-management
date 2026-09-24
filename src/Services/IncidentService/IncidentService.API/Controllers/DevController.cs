using BuildingBlocks.SharedKernel;
using IncidentService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IncidentService.API.Controllers;

/// <summary>
/// Development-only. Every action checks the environment itself and returns 404 outside
/// Development, so the endpoint does not exist as far as anything else is concerned.
///
/// Recorded in production_necessaries.MD §2 alongside the other dev-only endpoints: the guard is
/// real, but a production image would be cleaner without the controller in it at all.
/// </summary>
[ApiController]
[Route("api/dev")]
[ApiExplorerSettings(IgnoreApi = true)]
// Opts out of the fallback policy because there is no user here to have a token: this is run by
// hand or by a script, against a service that only answers at all in Development. The environment
// check below is the guard, and it is a stronger one than authentication would be.
[AllowAnonymous]
public sealed class DevController : ControllerBase
{
    private readonly DemoIncidentSeeder _seeder;
    private readonly IWebHostEnvironment _environment;
    private readonly IOrganizationContext _organization;

    public DevController(
        DemoIncidentSeeder seeder,
        IWebHostEnvironment environment,
        IOrganizationContext organization
    )
    {
        _seeder = seeder;
        _environment = environment;
        _organization = organization;
    }

    /// <summary>
    /// Backdated, varied incidents so the dashboard has a shape to draw. Idempotent: a second
    /// call reports what the first one left rather than doubling it.
    /// </summary>
    [HttpPost("seed-demo-incidents")]
    public async Task<IActionResult> SeedDemoIncidents(
        [FromQuery] Guid organizationId,
        CancellationToken cancellationToken
    )
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        // There is no user behind this endpoint and therefore no claim to take a scope from, so
        // whoever calls it has to say whose demo data they want. Asked for rather than defaulted:
        // an organisation this endpoint picked for itself would be a guess, and seeding a guess
        // is how demo rows end up in a customer's dashboard.
        if (organizationId == Guid.Empty)
            return BadRequest(
                new { error = "organizationId is required: say which organisation to seed." }
            );

        // The caller is the scope's writer here, for the same reason the polling loop is for
        // telemetry — nothing upstream has one to hand down. The seeder's idempotency check
        // then runs under the query filter, so each organisation is seeded once, independently.
        _organization.Set(organizationId);

        var result = await _seeder.SeedAsync(cancellationToken);

        return Ok(
            new
            {
                created = result.Created,
                alreadyPresent = result.AlreadyPresent,
                marker = DemoIncidentSeeder.Marker,
            }
        );
    }
}
