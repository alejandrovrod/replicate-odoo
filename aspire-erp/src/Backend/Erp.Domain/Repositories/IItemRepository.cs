using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the Item aggregate (decision D8). Interfaces live in Erp.Domain so
/// Erp.Application never references EF Core - Constitution Article I.3.
/// Implemented by Erp.Infrastructure.Data.Repositories.ItemRepository.
/// </summary>
/// <remarks>
/// Tenant isolation is AUTOMATIC: implementations query through AppDbContext, whose global query
/// filter scopes every read to the current tenant (Constitution II.3 - manual
/// <c>.Where(e =&gt; e.TenantId == ...)</c> in these implementations is forbidden).
/// </remarks>
public interface IItemRepository
{
    /// <summary>Persists a new item (TenantId is stamped by AppDbContext, never passed here).</summary>
    Task AddAsync(Item item, CancellationToken cancellationToken = default);

    /// <summary>True when the SKU already exists anywhere in the tenant (DoD 3.1: unique per tenant).</summary>
    Task<bool> ExistsSkuAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>All items of the tenant - the source of the ItemList query.</summary>
    Task<IReadOnlyList<Item>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>The item with the given id, or null when it does not exist in this tenant.</summary>
    Task<Item?> GetByIdAsync(Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>The subset of the given ids that exists in this tenant (for line expansion).</summary>
    Task<IReadOnlyList<Item>> GetByIdsAsync(IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken = default);
}
