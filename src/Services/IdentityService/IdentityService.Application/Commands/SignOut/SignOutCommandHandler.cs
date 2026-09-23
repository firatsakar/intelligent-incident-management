using IdentityService.Application.Abstractions;
using MediatR;

namespace IdentityService.Application.Commands.SignOut;

public sealed class SignOutCommandHandler : IRequestHandler<SignOutCommand>
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ITokenGenerator _tokens;

    public SignOutCommandHandler(IRefreshTokenRepository refreshTokens, ITokenGenerator tokens)
    {
        _refreshTokens = refreshTokens;
        _tokens = tokens;
    }

    public async Task Handle(SignOutCommand request, CancellationToken cancellationToken)
    {
        // Signing out without a refresh cookie is not a failure. The cookies are cleared either
        // way, and there is no version of this the caller needs to hear about.
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return;

        var token = await _refreshTokens.GetByHashAsync(
            _tokens.HashRefreshToken(request.RefreshToken),
            cancellationToken
        );

        if (token is null)
            return;

        token.Revoke(DateTime.UtcNow);

        await _refreshTokens.SaveChangesAsync(cancellationToken);
    }
}
