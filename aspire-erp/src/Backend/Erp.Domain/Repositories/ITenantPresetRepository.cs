using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the applied-preset record (Fase 5). Implemented by
/// Erp.Infrastructure. Tenant isolation is AUTOMATIC (Constitution II.3): at most one
/// record per tenant.
/// </summary>
public interface ITenantPresetRepository
{
    /// <summary>The tenant's applied preset record, or null when no preset was applied.</summary>
    Task<TenantPreset?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Upserts the tenant's applied preset record.</summary>
    Task SetAsync(string presetKey, CancellationToken cancellationToken = default);
}
