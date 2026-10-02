using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// The supplier's bill (Task 4.3 / spec BY-01). NO draft lifecycle: posting clears the interim
/// liability by debiting <c>Stock Received But Not Billed</c> at RECEIPT value, books the price
/// difference when the billed rate differs from the received rate, debits
/// <c>Input Tax Recoverable</c> for <see cref="TaxAmount"/> and credits <c>Accounts Payable</c>
/// for the gross - e.g. BY-01: 10 @ $100 + $100 VAT =&gt; Dr 2120 $1,000 / Dr tax $100 / Cr 2110 $1,100.
/// </summary>
/// <remarks>
/// THREE-WAY FULL MATCH (v1): exactly ONE invoice per PurchaseReceipt (unique index) and every
/// invoice line bills its receipt line IN FULL (same quantity; the rate is the vendor's and may
/// differ - the delta is expensed/credited through the price difference account). That makes the
/// interim liability for the receipt zero out deterministically (tasks.md 4.3 acceptance).
/// When the receipt fulfills an order, the order advances to <see cref="PurchaseOrderStatus.Billed"/>.
/// </remarks>
public class PurchaseInvoice : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    /// <summary>Receipt being billed (required in v1 - the three-way match anchor).</summary>
    public Guid PurchaseReceiptId { get; set; }

    public PurchaseReceipt? PurchaseReceipt { get; set; }

    /// <summary>Accounting date of the accrual reversal and the payable (GL lines).</summary>
    public DateOnly PostingDate { get; set; }

    /// <summary>
    /// Total Input Tax Recoverable of the bill (decimal(18,4), >= 0). Zero books no tax line;
    /// the amount is stated by the vendor (spec BY-01: "$100.00 VAT"), not computed from a rate.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Gapless voucher number (Constitution III.4): PINV-2026-00001.
    /// Assigned inside the posting transaction - the entity is only persisted with a number.
    /// </summary>
    public string VoucherNo { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<PurchaseInvoiceLine> Lines { get; set; } = new List<PurchaseInvoiceLine>();
}

/// <summary>
/// One line of a <see cref="PurchaseInvoice"/>: bills one receipt line IN FULL
/// (<see cref="Qty"/> must equal the receipt line's quantity; <see cref="Rate"/> is the vendor's).
/// </summary>
public class PurchaseInvoiceLine
{
    public Guid Id { get; set; }

    public Guid PurchaseInvoiceId { get; set; }

    public PurchaseInvoice? PurchaseInvoice { get; set; }

    /// <summary>Receipt line this invoice line settles - the three-way match anchor.</summary>
    public Guid PurchaseReceiptLineId { get; set; }

    public PurchaseReceiptLine? PurchaseReceiptLine { get; set; }

    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    /// <summary>Billed quantity (decimal(18,4), strictly positive, EQUAL to the receipt line's qty).</summary>
    public decimal Qty { get; set; }

    /// <summary>Billed unit rate (decimal(18,6), >= 0): the vendor's price; may differ from the received rate.</summary>
    public decimal Rate { get; set; }

    /// <summary>1-based line number inside the invoice.</summary>
    public int LineNumber { get; set; }
}
