using Erp.Domain.Entities.Security;

namespace Erp.Application.Features.Security.Commands;

/// <summary>
/// Shared construction helper for MFA handlers: builds <see cref="UserRecoveryCode"/> rows
/// from code hashes. Internal so the hashing contract (<see cref="Services.IRecoveryCodeGenerator"/>)
/// stays behind the handlers.
/// </summary>
internal static class MfaHandlerSupport
{
    public static UserRecoveryCode ToRecoveryCode(User user, string codeHash) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = user.TenantId,
            UserId = user.Id,
            CodeHash = codeHash,
            CreatedAt = DateTimeOffset.UtcNow,
            UsedAt = null,
        };
}
