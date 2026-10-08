using Erp.Domain.Common;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// The supplier's bill (Task 4.3 / spec BY-01). NO draft lifecycle: posting clears the interim
/// liability by debiting <c>Stock Received But Not Billed</c> at RECEIPT value, books the price
/// difference when the billed rate differs from the received rate, debits
/// <c>Input Tax Recoverable</c> for <see cref="TaxAmount"/> and credits <c>Accounts Payable</c>
/// for the gross - e.g. BY-01: 10 @ $100 + $100 VAT =&gt; Dr 2120 $1,000 / Dr tax $100 / Cr 2110 $1,100.
/// </summary>
/// The supplier's bill (Task 4.4).
/// </summary>
public enum PurchaseInvoiceStatus
{
    Draft = 1,
    Unpaid = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Cancelled = 5
}

public class PurchaseInvoice : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public string BillNumber { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public Guid? CurrencyId { get; set; }
    public Currency? Currency { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;


    /// <summary>Accounting date of the accrual reversal and the payable (GL lines).</summary>
    public DateOnly PostingDate { get; set; }

    public DateOnly DueDate { get; set; }
    
    public PurchaseInvoiceStatus Status { get; set; } = PurchaseInvoiceStatus.Draft;

    public decimal NetTotal { get; set; }
    
    public decimal TaxTotal { get; set; }
    
    public decimal WithholdingTaxTotal { get; set; }
    
    public decimal GrandTotal { get; set; }
    
    public decimal OutstandingAmount { get; set; }

    /// <summary>
    /// Gapless voucher number (Constitution III.4): PINV-2026-00001.
    /// Assigned inside the posting transaction - the entity is only persisted with a number.
    /// </summary>
    public string VoucherNo { get; set; } = string.Empty;

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<PurchaseInvoiceLine> Lines { get; set; } = new List<PurchaseInvoiceLine>();

    /// <summary>
    /// Cancels the bill and applies the compensating status transition (spec BY-05):
    /// <c>Unpaid</c>/<c>PartiallyPaid</c> =&gt; <c>Cancelled</c>. Ledger reversal is booked by
    /// <c>CancelPurchaseInvoiceCommandHandler</c>.
    /// </summary>
    /// <exception cref="PurchaseValidationException">
    /// <see cref="PurchaseErrorCodes.InvoiceAlreadyCancelled"/> when already cancelled, or
    /// <see cref="PurchaseErrorCodes.InvalidStatusTransition"/> for <see cref="PurchaseInvoiceStatus.Draft"/>
    /// and <see cref="PurchaseInvoiceStatus.Paid"/> (payments must be refunded first, BY-05).
    /// </exception>
    public void Cancel()
    {
        switch (Status)
        {
            case PurchaseInvoiceStatus.Cancelled:
                throw new PurchaseValidationException(PurchaseErrorCodes.InvoiceAlreadyCancelled,
                    $"Purchase invoice {VoucherNo} is already cancelled.");
            case PurchaseInvoiceStatus.Unpaid:
            case PurchaseInvoiceStatus.PartiallyPaid:
                Status = PurchaseInvoiceStatus.Cancelled;
                break;
            default:
                throw new PurchaseValidationException(PurchaseErrorCodes.InvalidStatusTransition,
                    $"Purchase invoice {VoucherNo} cannot be cancelled from status {Status}.");
        }
    }
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

    public decimal Amount { get; set; }

    /// <summary>1-based line number inside the invoice.</summary>
    public int LineNumber { get; set; }
}
