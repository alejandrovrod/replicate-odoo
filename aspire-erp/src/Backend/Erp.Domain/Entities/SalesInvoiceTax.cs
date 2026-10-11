using System;

namespace Erp.Domain.Entities;

/// <summary>
/// One row of the "Taxes and Charges" table of a sales invoice (module 17-sales-taxes-discounts,
/// ERPNext parity: default charge type "On Net Total"). Like <see cref="SalesInvoiceItem"/> the
/// row carries no <c>TenantId</c> of its own - tenancy rides on the parent
/// <see cref="SalesInvoice"/> (Constitution Article II.1).
/// </summary>
/// <remarks>
/// Amounts are ALWAYS stored non-negative: the taxable base of a return is negative, so the
/// computed tax is negative too and the posting engine books explicit inverse sides with
/// absolute magnitudes (GLEntry forbids negative Debit/Credit - Constitution IV.3).
/// </remarks>
public sealed class SalesInvoiceTax
{
    public Guid Id { get; set; }

    public Guid SalesInvoiceId { get; set; }
    public SalesInvoice? SalesInvoice { get; set; }

    /// <summary>Liability (tax payable) leaf account credited on submit (spec §3).</summary>
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    /// <summary>Percentage applied on the discounted net base (e.g. 21.00).</summary>
    public decimal Rate { get; set; }

    /// <summary>Server-computed tax amount (never trusted from the client).</summary>
    public decimal TaxAmount { get; set; }
}
