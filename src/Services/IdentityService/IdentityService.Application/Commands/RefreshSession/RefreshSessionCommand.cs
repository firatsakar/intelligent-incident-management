using IdentityService.Application.DTOs;
using MediatR;

namespace IdentityService.Application.Commands.RefreshSession;

/// <summary>Null means the presented token bought nothing, whatever the reason.</summary>
public sealed record RefreshSessionCommand(string RefreshToken) : IRequest<IssuedSession?>;
