using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Services;

/// <summary>One available batch with its derived on-hand quantity (FEFO source row).</summary>
public sealed record BatchAvailability(
    Guid BatchId,
    string BatchNo,
    DateOnly? ManufacturingDate,
    DateOnly? ExpiryDate,
    bool IsDisabled,
    decimal Qty,
    bool IsExpired);

/// <summary>
/// Shared batch/serial tracking operations (batch-and-serial Fase 3): master resolution,
/// movement gates and state transitions used by the stock, purchase-receipt and delivery
/// posting engines. Pure orchestration over repositories - valuation and GL stay in the
/// engines.
/// </summary>
/// <remarks>
/// <para>Batch balances are DERIVED from <c>StockLedgerEntry</c> rows (plain SUMs), never
/// stored: cancellations revert batch quantities automatically through their compensating
/// rows, so no quantity column can drift.</para>
/// <para>Expiry is evaluated against the movement's <c>PostingDate</c> (deterministic and
/// consistent with the fiscal locks), not wall-clock time. Disabled or expired batches
/// never dispatch, even when the company allows negative stock.</para>
/// <para>Serials are globally unique per tenant. A receipt rejects ANY pre-existing number
/// (documented Fase 3 boundary: no customer-returns re-entry flow exists yet), which keeps
/// the state machine invertible - receipt-cancel deletes, issue-cancel reactivates,
/// transfer-cancel moves back.</para>
/// </remarks>
public interface IBatchSerialService
{
    /// <summary>Find-or-create the receipt batch (top-up when it exists and is enabled).</summary>
    Task<Batch> EnsureBatchForReceiptAsync(
        Guid itemId,
        string itemCode,
        string batchNo,
        DateOnly? manufacturingDate,
        DateOnly? expiryDate,
        CancellationToken cancellationToken = default);

    /// <summary>Gate an issue/transfer line against its batch (existence, item, enabled, expiry, sufficiency).</summary>
    Task<Batch> EnsureBatchForIssueAsync(
        Guid itemId,
        string itemCode,
        Guid batchId,
        Guid warehouseId,
        string warehouseCode,
        decimal qty,
        DateOnly postingDate,
        bool allowNegativeStock,
        IReadOnlyDictionary<Guid, decimal>? consumedByBatch = null,
        CancellationToken cancellationToken = default);

    /// <summary>FEFO-ordered availability of one item in one warehouse (expiry ASC, nulls last).</summary>
    Task<IReadOnlyList<BatchAvailability>> GetAvailabilityAsync(
        Guid itemId,
        Guid warehouseId,
        DateOnly asOf,
        CancellationToken cancellationToken = default);

    /// <summary>Stage receipt serials (Active in the destination warehouse, doc refs stamped later).</summary>
    Task<IReadOnlyList<SerialNo>> StageReceiptSerialsAsync(
        Guid itemId,
        string itemCode,
        IReadOnlyList<string> serials,
        Guid warehouseId,
        Guid? batchId,
        DateOnly postingDate,
        CancellationToken cancellationToken = default);

    /// <summary>Validate and stage an issue of serials (Delivered, doc refs stamped later).</summary>
    Task<IReadOnlyList<SerialNo>> StageIssueSerialsAsync(
        Guid itemId,
        string itemCode,
        IReadOnlyList<string> serials,
        Guid warehouseId,
        string warehouseCode,
        DateOnly postingDate,
        CancellationToken cancellationToken = default);

    /// <summary>Validate and stage a transfer of serials (warehouse moved, stays Active).</summary>
    Task<IReadOnlyList<SerialNo>> StageTransferSerialsAsync(
        Guid itemId,
        string itemCode,
        IReadOnlyList<string> serials,
        Guid sourceWarehouseId,
        string sourceWarehouseCode,
        Guid targetWarehouseId,
        CancellationToken cancellationToken = default);

    /// <summary>Persist staged serials (adds for receipts, updates for issues/transfers).</summary>
    Task PersistSerialsAsync(
        IReadOnlyList<SerialNo> toAdd,
        IReadOnlyList<SerialNo> toUpdate,
        CancellationToken cancellationToken = default);

    /// <summary>Delete receipt-created serials (stock-entry cancellation revert).</summary>
    Task DeleteSerialsAsync(
        IReadOnlyList<SerialNo> serials,
        CancellationToken cancellationToken = default);
}
