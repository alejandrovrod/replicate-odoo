namespace Erp.Application.Common;

/// <summary>
/// Scoped ambient access to the tenant of the current request (Constitution Article II.2).
/// Implemented by a Scoped DI service and injected into <c>AppDbContext</c> at instantiation.
/// </summary>
public interface ITenantProvider
{
    /// <summary>
    /// Returns the current tenant id, or <see cref="Guid.Empty"/> when no tenant context exists.
    /// Fail-closed: an empty id makes tenant-scoped queries return zero rows (see plan.md §2).
    /// </summary>
    Guid GetCurrentTenantId();

    /// <summary>True when a usable tenant has been set for this scope.</summary>
    bool HasTenant();

    /// <summary>Sets the tenant for the current scope. Called by TenantResolutionMiddleware.</summary>
    void SetCurrentTenantId(Guid tenantId);
}
