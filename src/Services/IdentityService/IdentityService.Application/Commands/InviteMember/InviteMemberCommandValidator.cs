using FluentValidation;

namespace IdentityService.Application.Commands.InviteMember;

public sealed class InviteMemberCommandValidator : AbstractValidator<InviteMemberCommand>
{
    public InviteMemberCommandValidator()
    {
        // Sign-in deliberately checks nothing about an address's shape; an invitation is different,
        // because an email is about to be sent to it and a typo would send it nowhere.
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
        RuleFor(x => x.Role).IsInEnum();
    }
}
