using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// The physical shipment document of spec SL-01/SL-04 (Task 5.2b, Amendment A1): it relieves
/// inventory through the FIFO engine and books Cost of Goods Sold, so - like
/// <see cref="PurchaseReceipt"/> - the row exists only ONCE it is posted: posting writes
/// StockLedgerEntry rows, balanced General Ledger lines (Debit <c>Company.CogsAccountCode</c> /
/// Credit the warehouse stock account) and the gapless DN-YYYY-NNNNN voucher inside ONE
/// transaction.
/// </summary>
/// <remarks>
/// plan.md §1.6: NO money columns - the stock value comes from the cost layers at posting time,
/// NOT from the order rate (contrast PurchaseReceiptLine, whose Rate values the incoming stock).
/// Every posted note advances its <see cref="SalesOrder"/> in the SAME transaction:
/// <c>DeliveredQuantity</c>, <c>DeliveredPercentage</c> and the workflow status.
/// </remarks>
public class DeliveryNote : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    /// <summary>
    /// Gapless voucher number (Constitution III.4): DN-2026-00001.
    /// Assigned inside the posting transaction - the entity is only persisted with a number.
    /// </summary>
    public string VoucherNo { get; set; } = string.Empty;

    /// <summary>Order this shipment fulfills (plan.md §1.6 FK_DeliveryNote_SalesOrder).</summary>
    public Guid SalesOrderId { get; set; }

    public SalesOrder? SalesOrder { get; set; }

    /// <summary>Warehouse the goods leave (the same warehouse semantics as a material issue).</summary>
    public Guid WarehouseId { get; set; }

    public Warehouse? Warehouse { get; set; }

    /// <summary>Accounting date of the movement (and of the GL/Kardex rows it produces).</summary>
    public DateOnly PostingDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c>): two concurrent deliveries of
    /// the same order are a read-modify-write race on the order lines, so EF turns a stale save
    /// into <c>DbUpdateConcurrencyException</c> instead of a lost update. Store-generated.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    public ICollection<DeliveryNoteLine> Lines { get; set; } = new List<DeliveryNoteLine>();
}

/// <summary>
/// One line of a <see cref="DeliveryNote"/> (plan.md §1.6): how many units of which item leave
/// the warehouse against WHICH order line - the anchor the SL-04 non-overdelivery guard checks.
/// </summary>
public class DeliveryNoteLine
{
    public Guid Id { get; set; }

    public Guid DeliveryNoteId { get; set; }

    public DeliveryNote? DeliveryNote { get; set; }

    /// <summary>Fulfilled <see cref="SalesOrderItem"/> (FK_DeliveryNoteLine_OrderLine).</summary>
    public Guid SalesOrderItemId { get; set; }

    public SalesOrderItem? SalesOrderItem { get; set; }

    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    /// <summary>Delivered quantity in the item's Base UOM (decimal(18,4), CK_DeliveryNoteLine_Qty &gt; 0).</summary>
    public decimal Qty { get; set; }
}
