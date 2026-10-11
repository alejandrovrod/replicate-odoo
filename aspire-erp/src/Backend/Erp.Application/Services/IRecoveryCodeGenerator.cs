namespace Erp.Application.Services;

/// <summary>
/// MFA backup (recovery) codes (spec 15-user-profile §3.2: 10 codes per generation, old codes
/// invalidated). Plaintext codes are returned to the caller exactly once; only their SHA-256
/// hashes are persisted (spec §2: sensitive tokens must be hashed before persistence).
/// BCL-only (<c>RandomNumberGenerator</c> + SHA-256) so Erp.Application keeps zero NuGet
/// dependencies (Constitution I.3).
/// </summary>
public interface IRecoveryCodeGenerator
{
    /// <summary>Generates fresh plaintext codes (default 10, see <c>ProfileRules</c>).</summary>
    IReadOnlyList<string> Generate(int count);

    /// <summary>SHA-256 hex of the normalized code (uppercase, separators stripped).</summary>
    string Hash(string code);

    /// <summary>Normalizes a user-supplied code for comparison (uppercase, separators stripped).</summary>
    string Normalize(string code);
}
