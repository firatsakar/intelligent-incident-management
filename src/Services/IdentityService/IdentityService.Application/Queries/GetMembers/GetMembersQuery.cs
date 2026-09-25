using IdentityService.Application.DTOs;
using MediatR;

namespace IdentityService.Application.Queries.GetMembers;

public sealed record GetMembersQuery : IRequest<IReadOnlyList<MemberDto>>;
