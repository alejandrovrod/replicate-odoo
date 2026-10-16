using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the <see cref="SerialNo"/> master (batch-and-serial Fase 3).
/// Implemented by Erp.Infrastructure. Tenant isolation is AUTOMATIC (Constitution II.3).
/// Serial numbers are globally unique per tenant (UQ_SerialNo_Tenant_Number).
/// </summary>
public interface ISerialNoRepository
{
    /// <summary>Serials with any of the given numbers (tenant-scoped), for existence checks.</summary>
    Task<IReadOnlyList<SerialNo>> GetByNumbersAsync(
        IReadOnlyCollection<string> serialNumbers, CancellationToken cancellationToken = default);

    /// <summary>Serials moved by any of the given stock-entry lines (cancellation revert).</summary>
    Task<IReadOnlyList<SerialNo>> GetByStockEntryItemIdsAsync(
        IReadOnlyCollection<Guid> stockEntryItemIds, CancellationToken cancellationToken = default);

    /// <summary>Serials created under one purchase document number (receipt-unwind revert).</summary>
    Task<IReadOnlyList<SerialNo>> GetByPurchaseDocumentNoAsync(
        string purchaseDocumentNo, CancellationToken cancellationToken = default);

    /// <summary>Active serials of one item in one warehouse (picker/validation source).</summary>
    Task<IReadOnlyList<SerialNo>> GetActiveByItemWarehouseAsync(
        Guid itemId, Guid warehouseId, CancellationToken cancellationToken = default);

    /// <summary>Persists new serials.</summary>
    Task AddRangeAsync(IReadOnlyList<SerialNo> serials, CancellationToken cancellationToken = default);

    /// <summary>Persists serial state transitions.</summary>
    Task UpdateRangeAsync(IReadOnlyList<SerialNo> serials, CancellationToken cancellationToken = default);

    /// <summary>Deletes serials (receipt-cancellation of never-moved rows only).</summary>
    Task DeleteRangeAsync(IReadOnlyList<SerialNo> serials, CancellationToken cancellationToken = default);
}
