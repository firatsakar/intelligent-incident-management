using IdentityService.Application.Abstractions;
using MediatR;

namespace IdentityService.Application.Queries.GetSetupStatus;

/// <summary>
/// Whether this installation still needs its first-run setup — true only while no user exists.
/// Says nothing else: not the code, not whether one was issued.
/// </summary>
public sealed record GetSetupStatusQuery : IRequest<bool>;

public sealed class GetSetupStatusQueryHandler : IRequestHandler<GetSetupStatusQuery, bool>
{
    private readonly IUserRepository _users;

    public GetSetupStatusQueryHandler(IUserRepository users)
    {
        _users = users;
    }

    public async Task<bool> Handle(GetSetupStatusQuery request, CancellationToken cancellationToken) =>
        !await _users.AnyAsync(cancellationToken);
}
