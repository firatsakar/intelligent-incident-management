using FluentValidation;

namespace IdentityService.Application.Commands.SignIn;

public sealed class SignInCommandValidator : AbstractValidator<SignInCommand>
{
    public SignInCommandValidator()
    {
        // Both rules are about a form that was submitted empty, which the person can see for
        // themselves. Nothing here checks whether the address looks like an address: a rule that
        // rejected an unusual but valid one would lock somebody out of their own account, and the
        // password check below is what actually decides.
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
