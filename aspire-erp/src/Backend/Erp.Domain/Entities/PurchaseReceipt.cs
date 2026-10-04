using Erp.Domain.Common;

namespace Erp.Domain.Entities;

public enum PurchaseReceiptStatus
{
    Draft = 1,
    Submitted = 2,
    Cancelled = 3
}

/// <summary>
/// Goods received from a supplier (Task 4.3). Has a Draft lifecycle.
/// Submitting it posts interim liability and increments physical stock.
/// </summary>
public class PurchaseReceipt : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string VoucherNo { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public Guid? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public DateOnly PostingDate { get; set; }

    public PurchaseReceiptStatus Status { get; set; } = PurchaseReceiptStatus.Draft;

    public decimal TotalAmount { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<PurchaseReceiptLine> Lines { get; set; } = new List<PurchaseReceiptLine>();
}

public class PurchaseReceiptLine
{
    public Guid Id { get; set; }

    public Guid PurchaseReceiptId { get; set; }
    public PurchaseReceipt? PurchaseReceipt { get; set; }

    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }

    public int LineNumber { get; set; }
}
