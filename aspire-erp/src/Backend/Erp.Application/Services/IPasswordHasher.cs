namespace Erp.Application.Services;

/// <summary>
/// PBKDF2 password hashing for the User Profile &amp; Security module (spec 15-user-profile).
/// BCL-only (<c>Rfc2898DeriveBytes</c>) so Erp.Application keeps zero NuGet dependencies
/// (Constitution I.3). Implementations must also accept the legacy plaintext values seeded
/// before hashing existed, re-hashing them on the next successful verification.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a plaintext password into a versioned, salted verifier string.</summary>
    string Hash(string password);

    /// <summary>
    /// Verifies a plaintext candidate against a stored verifier (hashed or legacy plaintext).
    /// </summary>
    bool Verify(string password, string storedVerifier);
}
