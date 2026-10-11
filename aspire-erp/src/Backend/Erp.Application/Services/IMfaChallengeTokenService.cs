using Erp.Domain.Entities.Security;

namespace Erp.Application.Services;

/// <summary>
/// Short-lived proof that step 1 of the login (password) already succeeded for a user with
/// MFA enabled (spec 16 §3.1: "un Token temporal cifrado si la arquitectura ... lo requiere").
/// The ticket binds the MFA challenge to the verified password: <c>login-mfa</c> accepts no
/// email-only challenge, so knowing an address alone can never probe TOTP/backup codes.
/// BCL + JWT bearer primitives only - the implementation lives in Erp.Infrastructure alongside
/// <c>JwtTokenGenerator</c> so Erp.Application keeps zero NuGet dependencies.
/// </summary>
public interface IMfaChallengeTokenService
{
    /// <summary>Mints a ticket for the user (single purpose, minutes-long lifetime).</summary>
    string GenerateTicket(User user);

    /// <summary>
    /// Validates signature, expiry and purpose. False (with empty GUIDs) when the ticket is
    /// missing, malformed, expired, tampered or minted for another purpose.
    /// </summary>
    bool TryValidateTicket(string? ticket, out Guid userId, out Guid tenantId);
}
