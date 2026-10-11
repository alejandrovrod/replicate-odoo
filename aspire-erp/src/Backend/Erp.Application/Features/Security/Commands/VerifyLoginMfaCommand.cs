using Erp.Application.Common;

namespace Erp.Application.Features.Security.Commands;

/// <summary>
/// Step 2 of the MFA login (spec 16 §3.2): redeems the password-proof <c>MfaTicket</c> from
/// step 1 with a TOTP code (6 digits) or a backup code (8 chars once normalized, dashes and
/// spaces stripped). On success returns the definitive JWT inside the standard
/// <see cref="LoginResponseDto"/> (<c>IsMfaRequired</c> false); on failure increments
/// <c>AccessFailedCount</c> toward lockout. The identity comes exclusively from the ticket -
/// there is deliberately NO email/user-id input (no challenge probing by address).
/// </summary>
public sealed record VerifyLoginMfaCommand(
    string MfaTicket,
    string MfaCode,
    Guid TenantId) : ICommand<Result<LoginResponseDto>>;
