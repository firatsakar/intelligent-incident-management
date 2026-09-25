using IdentityService.Application.Abstractions;
using IdentityService.Application.DTOs;
using IdentityService.Domain.Exceptions;
using IdentityService.Domain.Services;
using MediatR;

namespace IdentityService.Application.Queries.GetPasswordResetPreview;

public sealed record GetPasswordResetPreviewQuery(string Token) : IRequest<PasswordResetPreviewDto>;

public sealed class GetPasswordResetPreviewQueryHandler : IRequestHandler<GetPasswordResetPreviewQuery, PasswordResetPreviewDto>
{
    private readonly IPasswordResetRepository _resets;
    private readonly IUserRepository _users;

    public GetPasswordResetPreviewQueryHandler(IPasswordResetRepository resets, IUserRepository users)
    {
        _resets = resets;
        _users = users;
    }

    public async Task<PasswordResetPreviewDto> Handle(GetPasswordResetPreviewQuery request, CancellationToken cancellationToken)
    {
        var reset = await _resets.FindByTokenHashForResetAsync(OneTimeToken.Hash(request.Token), cancellationToken);

        if (reset is null || !reset.IsUsable(DateTime.UtcNow))
            throw new LinkNotFoundException();

        var user = await _users.GetByIdAsync(reset.UserId, cancellationToken);

        if (user is null || !user.IsActive)
            throw new LinkNotFoundException();

        return new PasswordResetPreviewDto(user.Email, user.DisplayName, reset.ExpiresAt);
    }
}
