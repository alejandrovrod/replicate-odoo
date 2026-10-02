using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// The three stock voucher types of the perpetual inventory engine
/// (.specify/spec.md §4 Module 3). Persisted as the enum NAME (nvarchar(20)),
/// matching the RootType precedent from plan.md §7.3.
/// </summary>
public enum StockEntryType
{
    /// <summary>Goods arrive: stock up, Debit stock asset / Credit Stock Received But Not Billed (ST-01).</summary>
    MaterialReceipt,

    /// <summary>Goods leave: stock down, FIFO cost flows to COGS (ST-02).</summary>
    MaterialIssue,

    /// <summary>Move between two warehouses of the same company; value is preserved.</summary>
    MaterialTransfer,
}

/// <summary>
/// A stock voucher header. THERE IS NO DRAFT LIFECYCLE in Phase 3 (documented simplification):
/// creating a stock entry posts it atomically - validation, FIFO valuation, StockLedgerEntry rows,
/// General Ledger lines and the gapless VoucherNo are all written inside ONE database
/// transaction (Constitution III.1/III.4).
/// </summary>
/// <remarks>
/// Company-scoped: a stock entry always belongs to the company that owns its warehouse.
/// </remarks>
public class StockEntry : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    /// <summary>Source warehouse for issues/transfers, target warehouse for receipts.</summary>
    public Guid WarehouseId { get; set; }

    public Warehouse? Warehouse { get; set; }

    /// <summary>Target warehouse; only set for <see cref="StockEntryType.MaterialTransfer"/>.</summary>
    public Guid? TargetWarehouseId { get; set; }

    public Warehouse? TargetWarehouse { get; set; }

    public StockEntryType EntryType { get; set; }

    /// <summary>Accounting date of the movement (and of the GL lines it produces).</summary>
    public DateOnly PostingDate { get; set; }

    /// <summary>
    /// Gapless voucher number (Constitution III.4): MR-2026-00001 / MI-2026-00001 / MT-2026-00001.
    /// Assigned inside the posting transaction - the entity is only persisted with a number.
    /// </summary>
    public string VoucherNo { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<StockEntryItem> Items { get; set; } = new List<StockEntryItem>();
}

/// <summary>One line of a <see cref="StockEntry"/>: how much of which item moves.</summary>
public class StockEntryItem
{
    public Guid Id { get; set; }

    public Guid StockEntryId { get; set; }

    public StockEntry? StockEntry { get; set; }

    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    /// <summary>Moved quantity in the item's Base UOM (decimal(18,4), strictly positive).</summary>
    public decimal Qty { get; set; }

    /// <summary>
    /// Unit rate (decimal(18,6)): user-supplied on receipts (required, > 0), FIFO-computed on
    /// issues and transfers, null until the posting engine fills it in.
    /// </summary>
    public decimal? Rate { get; set; }

    /// <summary>1-based line number inside the voucher.</summary>
    public int LineNumber { get; set; }
}
