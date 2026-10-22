using Microsoft.Extensions.Logging;

namespace Erp.Api.Common;

/// <summary>
/// Structured audit trail for tenant onboarding. Never logs passwords, hashes or temporary
/// credentials - only the ids and the public subdomain.
/// </summary>
public static partial class TenantAuditLog
{
    [LoggerMessage(
        EventId = 7101,
        Level = LogLevel.Information,
        Message = "Tenant registered. TenantId={TenantId} CompanyId={CompanyId} AdminUserId={AdminUserId} Subdomain={Subdomain}")]
    public static partial void TenantRegistered(
        ILogger logger,
        Guid tenantId,
        Guid companyId,
        Guid adminUserId,
        string subdomain);
}
