using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// One slice of a <see cref="PaymentEntry"/> applied to a sales invoice. Part of the payment
/// aggregate: created only through the guarded <see cref="PaymentEntry.Allocate"/> path so the
/// anti-overpayment invariant (task 6.1) can never be bypassed.
/// </summary>
public sealed class PaymentAllocation : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public Guid PaymentEntryId { get; set; }

    public PaymentEntry? PaymentEntry { get; set; }

    /// <summary>Invoice this slice pays (table <c>SalesInvoice</c>).</summary>
    public Guid SalesInvoiceId { get; set; }

    public SalesInvoice? SalesInvoice { get; set; }

    /// <summary>Allocated amount (decimal(18,4), strictly positive).</summary>
    public decimal AllocatedAmount { get; set; }
}
