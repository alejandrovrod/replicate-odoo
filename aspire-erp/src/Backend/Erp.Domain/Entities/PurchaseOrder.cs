using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Procurement workflow of Task 4.1 (.specify/spec.md §6): Draft -&gt; Ordered -&gt; Received -&gt; Billed.
/// Persisted as the enum NAME (nvarchar(20)), matching the StockEntryType/RootType precedent.
/// </summary>
public enum PurchaseOrderStatus
{
    /// <summary>Created but not yet placed with the supplier; editable, no stock or GL impact.</summary>
    Draft,

    /// <summary>Submitted/confirmed - the supplier has the order. A PurchaseReceipt may reference it.</summary>
    Ordered,

    /// <summary>A PurchaseReceipt posted against the order: goods arrived, interim liability booked.</summary>
    Received,

    /// <summary>A PurchaseInvoice cleared the receipt: interim liability zeroed, Accounts Payable booked.</summary>
    Billed,
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

    /// <summary>Order date (also the reference date for the sequence year of the voucher).</summary>
    public DateOnly PostingDate { get; set; }

    /// <summary>
    /// Gapless voucher number (Constitution III.4): PO-2026-00001. Assigned inside the creation
    /// transaction - Draft orders already carry their number, like ERPNext's naming series on save.
    /// </summary>
    public string VoucherNo { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c> - spec BY-06): the workflow
    /// (Draft -&gt; Ordered -&gt; Received -&gt; Billed) is a read-modify-write, so EF puts the
    /// original value in the UPDATE ... WHERE clause and a concurrent transition between the load
    /// and the save throws <c>DbUpdateConcurrencyException</c> instead of being silently lost.
    /// Store-generated: never set from code.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}

/// <summary>One line of a <see cref="PurchaseOrder"/>: how much of which item to buy, at which committed rate.</summary>
public class PurchaseOrderLine
{
    public Guid Id { get; set; }

    public Guid PurchaseOrderId { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }

    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    /// <summary>Ordered quantity in the item's Base UOM (decimal(18,4), strictly positive).</summary>
    public decimal Qty { get; set; }

    /// <summary>Committed unit rate (decimal(18,6), strictly positive) - the price the supplier accepted.</summary>
    public decimal Rate { get; set; }

    /// <summary>1-based line number inside the order.</summary>
    public int LineNumber { get; set; }
}
