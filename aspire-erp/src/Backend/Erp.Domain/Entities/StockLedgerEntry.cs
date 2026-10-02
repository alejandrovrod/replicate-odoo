using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Append-only Kardex row (ubiquitous language: "Stock Ledger Entry" - .specify/spec.md §4).
/// One row per stock movement per warehouse: <see cref="QtyChange"/> and <see cref="Amount"/>
/// are SIGNED (+ for receipts into the warehouse, - for issues/transfers out of it), so the
/// on-hand balance and stock value of an (item, warehouse) pair are plain SUMs over the rows.
/// </summary>
/// <remarks>
/// The rows are the single source of truth for the FIFO engine: <c>Erp.Domain.Services.FifoValuation</c>
/// rebuilds the open cost layers from them. Rows are never updated or deleted - corrections happen
/// through compensating movements (Constitution III.3 spirit).
/// </remarks>
public class StockLedgerEntry : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    /// <summary>Warehouse the movement applies to (the source warehouse for transfers).</summary>
    public Guid WarehouseId { get; set; }

    public Warehouse? Warehouse { get; set; }

    /// <summary>Voucher that produced this row.</summary>
    public Guid StockEntryId { get; set; }

    public StockEntry? StockEntry { get; set; }

    public DateOnly PostingDate { get; set; }

    /// <summary>Signed quantity change (decimal(18,4)): positive = stock in, negative = stock out.</summary>
    public decimal QtyChange { get; set; }

    /// <summary>Valuation rate this row was booked at (decimal(18,6)).</summary>
    public decimal ValuationRate { get; set; }

    /// <summary>Signed value change (decimal(18,4)): the GL amount this row mirrors.</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Insert timestamp; also the FIFO tie-breaker together with PostingDate (rows are ordered by
    /// PostingDate, then CreatedAt, then Id).
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
