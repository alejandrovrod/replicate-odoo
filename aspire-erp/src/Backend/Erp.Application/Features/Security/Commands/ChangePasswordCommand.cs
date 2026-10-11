using Erp.Application.Common;

namespace Erp.Application.Features.Security.Commands;

/// <summary>
/// Changes the authenticated user's password (module 15-user-profile, spec §3.1).
/// The current password must verify; a mismatch increments <c>AccessFailedCount</c> as a
/// brute-force guard and reports <c>AUTH_INVALID_PASSWORD</c> (never which half failed).
/// </summary>
public sealed record ChangePasswordCommand(
    Guid UserId,
    Guid TenantId,
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword) : ICommand<Result<Unit>>;
