using IdentityService.Application.Abstractions;
using IdentityService.Application.DTOs;
using IdentityService.Application.Sessions;
using MediatR;

namespace IdentityService.Application.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, SessionUserDto?>
{
    private readonly IUserRepository _users;
    private readonly IOrganizationRepository _organizations;

    public GetCurrentUserQueryHandler(
        IUserRepository users,
        IOrganizationRepository organizations
    )
    {
        _users = users;
        _organizations = organizations;
    }

    public async Task<SessionUserDto?> Handle(
        GetCurrentUserQuery request,
        CancellationToken cancellationToken
    )
    {
        var user = await _users.GetByIdAsync(request.UserId, cancellationToken);

        // A valid token for an account that has since been removed or switched off. The token
        // cannot be withdrawn, so this is where it stops being worth anything.
        if (user is null || !user.IsActive)
            return null;

        var organization = await _organizations.GetByIdAsync(user.OrganizationId, cancellationToken);

        return SessionIssuer.Describe(user, organization);
    }
}
