using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the <see cref="SupplierGroup"/> catalog (module buying-suppliers,
/// spec §1 gap D1). Implemented by Erp.Infrastructure.Data.Repositories.SupplierGroupRepository.
/// </summary>
/// <remarks>
/// Tenant isolation is AUTOMATIC (Constitution II.3): implementations query through AppDbContext,
/// whose global filter scopes every read to the current tenant.
/// </remarks>
public interface ISupplierGroupRepository
{
    /// <summary>Persists a new group (TenantId is stamped by AppDbContext, never passed here).</summary>
    Task AddAsync(SupplierGroup group, CancellationToken cancellationToken = default);

    /// <summary>True when the code already exists in this tenant.</summary>
    Task<bool> ExistsCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>The group with the given id, or null when it does not exist in this tenant.</summary>
    Task<SupplierGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Every group of the tenant, active first then alphabetical - the picker catalog.</summary>
    Task<IReadOnlyList<SupplierGroup>> GetActiveAsync(CancellationToken cancellationToken = default);
}
