using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Sales workflow of Task 5.2 (.specify/modules/03-selling/spec.md): Draft -&gt; Submitted -&gt;
/// PartiallyDelivered -&gt; Completed, with Cancelled reserved for the return/cancellation flow.
/// </summary>
/// <remarks>
/// plan.md §1 is explicit (<c>Status INT NOT NULL DEFAULT 1</c>) and the orchestrator confirmed
/// plan §1 WINS over the <see cref="PurchaseOrderStatus"/> name-precedent, so this enum is
/// persisted as the enum VALUE (int), not as the enum name (nvarchar(20)).
/// </remarks>
public enum SalesOrderStatus
{
    /// <summary>Created but not yet confirmed; editable, no stock, GL or credit exposure.</summary>
    Draft = 1,

    /// <summary>Confirmed against the customer: the credit gate has passed and delivery may start.</summary>
    Submitted = 2,

    /// <summary>At least one line was delivered, at least one still owes quantity.</summary>
    PartiallyDelivered = 3,

    /// <summary>Every line is fully delivered - fulfillment of this order is closed.</summary>
    Completed = 4,

    /// <summary>Cancelled before fulfillment (spec SL-05 vocabulary; no delivery may reference it).</summary>
    Cancelled = 5,
}

/// <summary>
/// A sales order header (Task 5.2): the confirmed customer commitment that authorizes fulfillment.
/// The row is created in <see cref="SalesOrderStatus.Draft"/> with its gapless OrderNumber
/// (SO-YYYY-NNNNN) assigned inside the creation transaction, advances only through the workflow,
/// and never posts to the General Ledger by itself - COGS is booked by the DeliveryNote
/// (Task 5.2b), revenue by the SalesInvoice (Task 5.3).
/// </summary>
/// <remarks>
/// plan.md §1 (authoritative DDL): company-scoped, one customer per order
/// (FK_SalesOrder_Customer), money columns are the document totals (no tax engine yet - TaxTotal
/// stays 0.0000 until Task 5.3) and the two percentage columns track fulfillment/billing progress
/// as quantity-weighted ratios in decimal(5,2).
/// </remarks>
public class SalesOrder : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }

    /// <summary>Workflow state (Task 5.2 acceptance: partial/full deliveries drive this field).</summary>
    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Draft;

    /// <summary>Order date (also the reference date for the sequence year of the order number).</summary>
    public DateOnly TransactionDate { get; set; }

    /// <summary>Promised shipment date - the delivery-date tracking of Task 5.2.</summary>
    public DateOnly DeliveryDate { get; set; }

    /// <summary>
    /// Gapless order number (Constitution III.4): SO-2026-00001. Assigned inside the creation
    /// transaction - Draft orders already carry their number, like ERPNext's naming series on save.
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>Sum of the line amounts (decimal(18,4), CK_SalesOrder_Totals).</summary>
    public decimal NetTotal { get; set; }

    /// <summary>Tax component of the total; 0.0000 until the tax engine lands (Task 5.3).</summary>
    public decimal TaxTotal { get; set; }

    /// <summary>NetTotal + TaxTotal - the amount the credit gate evaluates (spec SL-02).</summary>
    public decimal GrandTotal { get; set; }

    /// <summary>
    /// Quantity-weighted fulfillment ratio: <c>sum(DeliveredQuantity) / sum(Quantity) * 100</c>
    /// rounded to 2 decimals, recomputed by every posted DeliveryNote (Task 5.2b).
    /// </summary>
    public decimal DeliveredPercentage { get; set; }

    /// <summary>
    /// Quantity-weighted billing ratio (decimal(5,2), plan.md §1 default 0.00). Only Task 5.3's
    /// SalesInvoice moves it - the delivery flow NEVER touches it.
    /// </summary>
    public decimal BilledPercentage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c>): the workflow and the
    /// fulfillment update are read-modify-writes, so EF puts the original value in the
    /// UPDATE ... WHERE clause and a concurrent transition throws
    /// <c>DbUpdateConcurrencyException</c> instead of being silently lost.
    /// Store-generated: never set from code.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    public ICollection<SalesOrderItem> Lines { get; set; } = new List<SalesOrderItem>();
}

/// <summary>
/// One line of a <see cref="SalesOrder"/> (plan.md §1 SalesOrderItem): how much of which item the
/// customer committed to, at which rate, plus the two running fulfillment counters the DeliveryNote
/// increments (SL-04 evaluates <c>Quantity - DeliveredQuantity</c>).
/// </summary>
public class SalesOrderItem
{
    public Guid Id { get; set; }

    public Guid SalesOrderId { get; set; }

    public SalesOrder? SalesOrder { get; set; }

    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    /// <summary>Ordered quantity in the item's Base UOM (decimal(18,4), CK_SalesOrderItem_Quantity &gt; 0).</summary>
    public decimal Quantity { get; set; }

    /// <summary>Units already relieved by posted DeliveryNotes (decimal(18,4), default 0.0000).</summary>
    public decimal DeliveredQuantity { get; set; }

    /// <summary>Units already billed by posted SalesInvoices (default 0.0000; Task 5.3 writes it).</summary>
    public decimal BilledQuantity { get; set; }

    /// <summary>Committed unit rate (decimal(18,4), CK_SalesOrderItem_Rate &gt;= 0 - a line may be free).</summary>
    public decimal Rate { get; set; }

    /// <summary>Line amount (decimal(18,4), CK_SalesOrderItem_Amount &gt;= 0): Round4(Quantity * Rate).</summary>
    public decimal Amount { get; set; }
}
