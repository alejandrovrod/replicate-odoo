using Microsoft.Extensions.Logging;

namespace Erp.Api.Common;

/// <summary>
/// Structured audit trail for User Management mutations (user-management spec D2/D4). The
/// project has no audit table, so administrative actions are emitted as <c>[LoggerMessage]</c>
/// events (Constitution V.2: source-generated, never string interpolation) and inherit the
/// ambient TenantId/CorrelationId/UserId scope opened by <c>TenantLoggingScopeMiddleware</c>.
/// </summary>
/// <remarks>
/// Passwords never reach this class: create/reset events log the target user and the actor
/// only, never the plaintext or its hash.
/// </remarks>
public static partial class UserAuditLog
{
    [LoggerMessage(
        EventId = 5100,
        Level = LogLevel.Information,
        Message = "User created. Actor={ActorId} Tenant={TenantId} Target={TargetUserId} Email={TargetEmail} IsActive={IsActive} Roles={RoleCount}")]
    public static partial void UserCreated(
        ILogger logger,
        Guid actorId,
        Guid tenantId,
        Guid targetUserId,
        string targetEmail,
        bool isActive,
        int roleCount);

    [LoggerMessage(
        EventId = 5101,
        Level = LogLevel.Information,
        Message = "User updated. Actor={ActorId} Tenant={TenantId} Target={TargetUserId} IsActive={IsActive} Roles={RoleCount}")]
    public static partial void UserUpdated(
        ILogger logger,
        Guid actorId,
        Guid tenantId,
        Guid targetUserId,
        bool isActive,
        int roleCount);

    [LoggerMessage(
        EventId = 5102,
        Level = LogLevel.Information,
        Message = "User status changed. Actor={ActorId} Tenant={TenantId} Target={TargetUserId} IsActive={IsActive}")]
    public static partial void UserStatusChanged(
        ILogger logger,
        Guid actorId,
        Guid tenantId,
        Guid targetUserId,
        bool isActive);

    [LoggerMessage(
        EventId = 5103,
        Level = LogLevel.Warning,
        Message = "User password reset. Actor={ActorId} Tenant={TenantId} Target={TargetUserId}")]
    public static partial void UserPasswordReset(
        ILogger logger,
        Guid actorId,
        Guid tenantId,
        Guid targetUserId);
}
