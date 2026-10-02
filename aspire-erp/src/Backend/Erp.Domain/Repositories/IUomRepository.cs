using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for UOM lookups (decision D8). Units of measure are seeded, not edited
/// through the API in Phase 3, so the contract is intentionally read-only.
/// </summary>
public interface IUomRepository
{
    /// <summary>The unit of measure with the given id, or null when it does not exist in this tenant.</summary>
    Task<UOM?> GetByIdAsync(Guid uomId, CancellationToken cancellationToken = default);
}
