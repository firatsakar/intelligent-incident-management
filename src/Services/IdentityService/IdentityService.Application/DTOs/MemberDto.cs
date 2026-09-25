using IdentityService.Domain.Aggregates;
using IdentityService.Domain.Enums;

namespace IdentityService.Application.DTOs;

public sealed record MemberDto(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt
)
{
    public static MemberDto FromDomain(User user) =>
        new(user.Id, user.Email, user.DisplayName, user.Role, user.IsActive, user.CreatedAt);
}
