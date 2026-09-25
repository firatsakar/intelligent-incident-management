using BuildingBlocks.SharedKernel;
using IdentityService.Application.Abstractions;
using IdentityService.Application.DTOs;
using MediatR;

namespace IdentityService.Application.Queries.GetMembers;

public sealed class GetMembersQueryHandler : IRequestHandler<GetMembersQuery, IReadOnlyList<MemberDto>>
{
    private readonly IUserRepository _users;
    private readonly IOrganizationContext _organization;

    public GetMembersQueryHandler(IUserRepository users, IOrganizationContext organization)
    {
        _users = users;
        _organization = organization;
    }

    public async Task<IReadOnlyList<MemberDto>> Handle(GetMembersQuery request, CancellationToken cancellationToken)
    {
        var users = await _users.ListByOrganizationAsync(_organization.Required, cancellationToken);

        return users.Select(MemberDto.FromDomain).ToList();
    }
}
