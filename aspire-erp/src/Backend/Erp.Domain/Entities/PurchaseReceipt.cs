using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Goods received from a supplier (Task 4.2). NO draft lifecycle - like <see cref="StockEntry"/>,
/// the row exists only once it is posted: posting writes +Kardex rows, balanced General Ledger
/// lines (Debit warehouse stock account / Credit <c>Stock Received But Not Billed</c>, spec ST-01
/// and tasks.md 4.2) and the gapless PR-YYYY-NNNNN voucher inside ONE transaction.
/// </summary>
/// <remarks>
/// When <see cref="PurchaseOrderId"/> is set, the linked order must be Ordered/Received and
/// advances to <see cref="PurchaseOrderStatus.Received"/> (Task 4.1 workflow). Direct purchases
/// without an order are allowed (PurchaseOrderId null) - the accrual posting does not depend on
/// an order.
/// </remarks>
public class PurchaseReceipt : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    /// <summary>Order this receipt fulfills; null for direct purchases.</summary>
    public Guid? PurchaseOrderId { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }

    /// <summary>Warehouse the goods land in (the same warehouse semantics as a material receipt).</summary>
    public Guid WarehouseId { get; set; }

    public Warehouse? Warehouse { get; set; }

    /// <summary>Accounting date of the movement (and of the GL/Kardex rows it produces).</summary>
    public DateOnly PostingDate { get; set; }

    /// <summary>
    /// Gapless voucher number (Constitution III.4): PR-2026-00001.
    /// Assigned inside the posting transaction - the entity is only persisted with a number.
    /// </summary>
    public string VoucherNo { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<PurchaseReceiptLine> Lines { get; set; } = new List<PurchaseReceiptLine>();
}

/// <summary>One line of a <see cref="PurchaseReceipt"/>: how much of which item arrived, at the received rate.</summary>
public class PurchaseReceiptLine
{
    public Guid Id { get; set; }

    public Guid PurchaseReceiptId { get; set; }

    public PurchaseReceipt? PurchaseReceipt { get; set; }

    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    /// <summary>Received quantity in the item's Base UOM (decimal(18,4), strictly positive).</summary>
    public decimal Qty { get; set; }

    /// <summary>Received unit rate (decimal(18,6), strictly positive) - it values the incoming stock (spec ST-01).</summary>
    public decimal Rate { get; set; }

    /// <summary>1-based line number inside the receipt.</summary>
    public int LineNumber { get; set; }
}
