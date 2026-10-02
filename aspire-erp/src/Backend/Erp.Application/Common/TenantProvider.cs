namespace Erp.Application.Common;

/// <summary>
/// Default scoped implementation: one tenant per DI scope (i.e. per HTTP request).
/// Registered as a Scoped service in Erp.Api's composition root.
/// </summary>
public sealed class TenantProvider : ITenantProvider
{
    private Guid _currentTenantId = Guid.Empty;

    public Guid GetCurrentTenantId() => _currentTenantId;

    public bool HasTenant() => _currentTenantId != Guid.Empty;

    public void SetCurrentTenantId(Guid tenantId) => _currentTenantId = tenantId;
}
