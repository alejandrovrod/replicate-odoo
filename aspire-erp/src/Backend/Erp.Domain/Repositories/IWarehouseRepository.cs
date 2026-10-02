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

    /// <summary>True when the code already exists within the given company (codes are unique per company).</summary>
    Task<bool> ExistsByCodeAsync(Guid companyId, string code, CancellationToken cancellationToken = default);
}
