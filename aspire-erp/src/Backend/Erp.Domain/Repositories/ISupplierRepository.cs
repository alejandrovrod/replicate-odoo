using Erp.Domain.Common;
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

    /// <summary>
    /// The supplier with the given id (with its <c>Accounts</c> and <c>SupplierGroup</c> loaded),
    /// or null when it does not exist in this tenant.
    /// </summary>
    Task<Supplier?> GetByIdAsync(Guid supplierId, CancellationToken cancellationToken = default);

    /// <summary>The requested suppliers (existing ones only) - resolves codes/names for order lists.</summary>
    Task<IReadOnlyList<Supplier>> GetByIdsAsync(IReadOnlyList<Guid> supplierIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// The most recent suppliers of the tenant (newest first) for the list view, optionally
    /// filtered by supplier group, hold status and the company that holds an account mapping
    /// (spec §5: <c>GET /api/v1/suppliers?groupId=&amp;onHold=&amp;companyId=</c>).
    /// </summary>
    Task<PagedResult<Supplier>> GetRecentAsync(
        PagedRequest paging,
        Guid? supplierGroupId = null,
        bool? onHold = null,
        Guid? companyId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Updates an existing supplier.</summary>
    Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default);
}
