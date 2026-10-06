using Erp.Domain.Common;
using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the Warehouse aggregate (decision D8). Implemented by
/// Erp.Infrastructure.Data.Repositories.WarehouseRepository; see IItemRepository for the
/// Constitution II.3 note on automatic tenant isolation.
/// </summary>
public interface IWarehouseRepository
{
    /// <summary>Persists a new warehouse (TenantId is stamped by AppDbContext).</summary>
    Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default);

    /// <summary>The warehouse with the given id, or null when it does not exist in this tenant.</summary>
    Task<Warehouse?> GetByIdAsync(Guid warehouseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the requested warehouse followed by its ancestors - parent, grandparent, ..., root
    /// (index 0 = the warehouse itself). Empty list when the warehouse does not exist (or belongs
    /// to another tenant, which the global query filter treats as non-existent).
    /// </summary>
    Task<IReadOnlyList<Warehouse>> GetByIdWithAncestorsAsync(Guid warehouseId, CancellationToken cancellationToken = default);

    /// <summary>All warehouses of one company - the flat source the tree query assembles into a hierarchy.</summary>
    Task<IReadOnlyList<Warehouse>> GetByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Flat paged warehouses for card grids (Standard Pagination Pattern): the tree endpoint
    /// stays hierarchical, this one servesLayouts that need rows. <c>leavesOnly</c> restricts to
    /// ledger warehouses (groups hold no stock); <c>isActive</c> optionally filters by status.
    /// Ordered by code for stable pages.
    /// </summary>
    Task<PagedResult<Warehouse>> GetFlatWarehousesAsync(
        Guid companyId,
        bool leavesOnly,
        bool? isActive,
        PagedRequest paging,
        CancellationToken cancellationToken = default);
    /// <summary>True when the code already exists within the given company (codes are unique per company).</summary>
    Task<bool> ExistsByCodeAsync(Guid companyId, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to a tracked warehouse. A concurrent modification between load and save
    /// surfaces as <c>DbUpdateConcurrencyException</c> (RowVersion WHERE clause) — implementations
    /// translate it into <c>ConcurrencyConflictException</c>, mirroring JournalRepository.
    /// </summary>
    Task UpdateAsync(Warehouse warehouse, CancellationToken cancellationToken = default);
}
