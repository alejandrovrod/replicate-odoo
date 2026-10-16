using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the <see cref="Batch"/> master (batch-and-serial Fase 3).
/// Implemented by Erp.Infrastructure. Tenant isolation is AUTOMATIC (Constitution II.3).
/// </summary>
public interface IBatchRepository
{
    /// <summary>The batch with the given id, or null outside this tenant.</summary>
    Task<Batch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The batch of one item with the given business number, or null.</summary>
    Task<Batch?> GetByItemAndNumberAsync(Guid itemId, string batchNo, CancellationToken cancellationToken = default);

    /// <summary>All batches of one item (FEFO ordering happens in the application layer).</summary>
    Task<IReadOnlyList<Batch>> GetByItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>Persists a new batch.</summary>
    Task AddAsync(Batch batch, CancellationToken cancellationToken = default);

    /// <summary>Persists an updated batch (disable/retire flow).</summary>
    Task UpdateAsync(Batch batch, CancellationToken cancellationToken = default);
}
