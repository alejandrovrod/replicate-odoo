using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// One slice of a <see cref="PaymentEntry"/> applied to exactly one invoice (spec R-12
/// invariant PE-06): either a <c>SalesInvoice</c> (Receive leg) or a <c>PurchaseInvoice</c>
/// (Pay leg), never both, enforced by <c>CK_PaymentAllocation_ExactlyOneInvoice</c> in
/// addition to the application guard. Part of the payment aggregate: created only through
/// the guarded <see cref="PaymentEntry.Allocate"/> path so the anti-overpayment invariant
/// (PE-02) can never be bypassed.
/// </summary>
public sealed class PaymentAllocation : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public Guid PaymentEntryId { get; set; }

    public PaymentEntry? PaymentEntry { get; set; }

    /// <summary>Invoice this slice pays (table <c>SalesInvoice</c>); null on the Pay leg.</summary>
    public Guid? SalesInvoiceId { get; set; }

    public SalesInvoice? SalesInvoice { get; set; }

    /// <summary>Bill this slice pays (table <c>PurchaseInvoice</c>); null on the Receive leg.</summary>
    public Guid? PurchaseInvoiceId { get; set; }

    public PurchaseInvoice? PurchaseInvoice { get; set; }

    /// <summary>Allocated amount (decimal(18,4), strictly positive).</summary>
    public decimal AllocatedAmount { get; set; }

    /// <summary>
    /// ERPNext parity (<c>reference_doctype</c>): "SalesInvoice" or "PurchaseInvoice" -
    /// denormalized from the FK leg for reporting without joins.
    /// </summary>
    public string ReferenceDocumentType { get; set; } = string.Empty;

    /// <summary>
    /// ERPNext parity: id of the referenced document (mirrors SalesInvoiceId/PurchaseInvoiceId
    /// for the active leg).
    /// </summary>
    public Guid? ReferenceDocumentId { get; set; }

    /// <summary>
    /// ERPNext parity: invoice grand total at allocation time (snapshot for the allocation grid).
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// ERPNext parity: invoice outstanding at allocation time (the PE-02 cap snapshot).
    /// </summary>
    public decimal OutstandingAmount { get; set; }

    /// <summary>
    /// ERPNext parity: invoice exchange rate at allocation time (snapshot for FX settlement).
    /// </summary>
    public decimal ExchangeRate { get; set; } = 1m;
}
