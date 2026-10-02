using Microsoft.Extensions.Logging;

namespace Erp.Infrastructure.Logging;

/// <summary>
/// High-performance compile-time log messages (plan.md §3.2). Constitution Article V.2: every
/// logger call uses the [LoggerMessage] source generator on this partial static class - runtime
/// string interpolation inside logger calls is prohibited.
/// </summary>
public static partial class TenantLogs
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Tenant {TenantId} resolved for request {RequestPath}")]
    public static partial void LogTenantResolved(this ILogger logger, Guid tenantId, string requestPath);
}
