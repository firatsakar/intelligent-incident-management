using System.Text;
using FluentValidation;

namespace IdentityService.Application.Accounts;

/// <summary>
/// What a password chosen from now on has to be. Sign-in checks none of this — an existing
/// password is whatever it was — so these rules apply where a password is set: accepting an
/// invitation, completing a reset, changing one's own.
/// </summary>
public static class PasswordRules
{
    public const int MinimumLength = 12;

    // BCrypt reads the first 72 bytes and ignores the rest. A longer password would be accepted and
    // then quietly matched by any other password sharing those bytes; refusing it is honest.
    public const int MaximumBytes = 72;

    public static IRuleBuilderOptions<T, string> NewPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty()
            .MinimumLength(MinimumLength)
            .WithMessage($"Use at least {MinimumLength} characters.")
            .Must(password => Encoding.UTF8.GetByteCount(password ?? string.Empty) <= MaximumBytes)
            .WithMessage($"Use at most {MaximumBytes} bytes; longer passwords are cut off by the hash.");
}
