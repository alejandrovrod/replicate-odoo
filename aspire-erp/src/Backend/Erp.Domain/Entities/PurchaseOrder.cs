using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Procurement workflow of Task 4.1 (.specify/spec.md §6): Draft -&gt; Ordered -&gt; Received -&gt; Billed.
/// Persisted as the enum NAME (nvarchar(20)), matching the StockEntryType/RootType precedent.
/// </summary>
public enum PurchaseOrderStatus
{
    Draft,
    Submitted,
    PartiallyReceived,
    Completed,
    Cancelled
}

/// <summary>
/// A purchase order header (Task 4.1). Unlike StockEntry/PurchaseReceipt/PurchaseInvoice there IS
/// a draft lifecycle here: the row is created in <see cref="PurchaseOrderStatus.Draft"/> with its
/// gapless VoucherNo (PO-YYYY-NNNNN) assigned inside the creation transaction, and it advances
/// only through the workflow - a purchase order NEVER posts to the General Ledger by itself
/// (accrual happens on receipt, Task 4.2; clearance happens on invoice, Task 4.3).
/// </summary>
/// <remarks>
/// Company-scoped: the order belongs to the company that will receive and pay for the goods.
/// Supplier is tenant-scoped (like Item), so the FK crosses the two scopes - same as Account's
/// Company FK on the other side.
/// </remarks>
public class PurchaseOrder : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    /// <summary>Workflow state (Task 4.1 acceptance: Draft -&gt; Ordered -&gt; Received -&gt; Billed).</summary>
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    public DateOnly TransactionDate { get; set; }

    public DateOnly ScheduleDate { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public decimal NetTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal ReceivedPercentage { get; set; }
    public decimal BilledPercentage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c> - spec BY-06): the workflow
    /// (Draft -&gt; Ordered -&gt; Received -&gt; Billed) is a read-modify-write, so EF puts the
    /// original value in the UPDATE ... WHERE clause and a concurrent transition between the load
    /// and the save throws <c>DbUpdateConcurrencyException</c> instead of being silently lost.
    /// Store-generated: never set from code.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}

/// <summary>One line of a <see cref="PurchaseOrder"/>: how much of which item to buy, at which committed rate.</summary>
public class PurchaseOrderItem
{
    public Guid Id { get; set; }

    public Guid PurchaseOrderId { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }

    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    /// <summary>Ordered quantity in the item's Base UOM (decimal(18,4), strictly positive).</summary>
    public decimal Quantity { get; set; }

    public decimal ReceivedQuantity { get; set; }
    public decimal BilledQuantity { get; set; }

    /// <summary>Committed unit rate (decimal(18,6), strictly positive) - the price the supplier accepted.</summary>
    public decimal Rate { get; set; }

    public decimal Amount { get; set; }

    /// <summary>1-based line number inside the order.</summary>
    public int LineNumber { get; set; }
}
