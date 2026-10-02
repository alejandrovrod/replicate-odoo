using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the Supplier master (Task 4.1). Implemented by
/// Erp.Infrastructure.Data.Repositories.SupplierRepository.
/// </summary>
/// <remarks>
/// Tenant isolation is AUTOMATIC (Constitution II.3): implementations query through AppDbContext,
/// whose global filter scopes every read to the current tenant. Suppliers are tenant-wide (like
/// Items), so reads are not company-filtered.
/// </remarks>
public interface ISupplierRepository
{
    /// <summary>Persists a new supplier (TenantId is stamped by AppDbContext, never passed here).</summary>
    Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default);

    /// <summary>True when the code already exists in this tenant (Task 4.1: unique SKU-style rule).</summary>
    Task<bool> ExistsCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>The supplier with the given id, or null when it does not exist in this tenant.</summary>
    Task<Supplier?> GetByIdAsync(Guid supplierId, CancellationToken cancellationToken = default);

    /// <summary>The requested suppliers (existing ones only) - resolves codes/names for order lists.</summary>
    Task<IReadOnlyList<Supplier>> GetByIdsAsync(IReadOnlyList<Guid> supplierIds, CancellationToken cancellationToken = default);

    /// <summary>The most recent suppliers of the tenant (newest first) for the list view.</summary>
    Task<IReadOnlyList<Supplier>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
}
