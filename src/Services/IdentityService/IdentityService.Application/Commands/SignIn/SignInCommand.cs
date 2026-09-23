using IdentityService.Application.DTOs;
using MediatR;

namespace IdentityService.Application.Commands.SignIn;

/// <summary>
/// Null means rejected, and deliberately says no more than that. Wrong password, unknown address
/// and a deactivated account are the same answer, because an endpoint that tells them apart is an
/// endpoint that confirms which addresses have accounts here.
/// </summary>
public sealed record SignInCommand(string Email, string Password) : IRequest<IssuedSession?>;
