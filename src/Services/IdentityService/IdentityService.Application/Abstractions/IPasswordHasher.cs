namespace IdentityService.Application.Abstractions;

/// <summary>
/// Turns a password into something storable, and checks one against what was stored.
/// </summary>
/// <remarks>
/// An interface rather than a static call to BCrypt so the work factor — the number this whole
/// scheme rests on — is a deployment decision in one place, and so nothing in the application
/// layer has to reference a hashing library to talk about a password.
/// </remarks>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// True when <paramref name="password"/> is the one behind <paramref name="hash"/>. Returns
    /// false rather than throwing on a hash it cannot read, because a row that predates a format
    /// change is a failed sign-in, not a failed request.
    /// </summary>
    bool Verify(string password, string hash);
}
