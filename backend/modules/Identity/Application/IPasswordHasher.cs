namespace SeniorConnect.Modules.Identity.Application;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);

    /// <summary>
    /// True when the stored hash uses a weaker work factor than the current
    /// policy and should be re-hashed after a successful login.
    /// </summary>
    bool NeedsRehash(string passwordHash);
}
