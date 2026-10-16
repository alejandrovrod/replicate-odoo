using System;

namespace Erp.Domain.Entities;

/// <summary>
/// One row of the "Taxes and Charges" table of a sales invoice (module 17-sales-taxes-discounts,
/// ERPNext parity: default charge type "On Net Total"). Like <see cref="SalesInvoiceItem"/> the
/// row carries no <c>TenantId</c> of its own - tenancy rides on the parent
/// <see cref="SalesInvoice"/> (Constitution Article II.1).
/// </summary>
/// <remarks>
/// Signed amounts (tax-engine Fase 2): <see cref="TaxChargeType.Add"/> rows follow the base
/// sign (positive on normal invoices, negative on returns); <see cref="TaxChargeType.Deduct"/>
/// rows (retentions) carry the OPPOSITE sign, so the document total is always
/// <c>NetTotal + Σ TaxAmount</c>. The posting engine books explicit sides from the sign
/// (GLEntry forbids negative Debit/Credit - Constitution IV.3).
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

    /// <summary>
    /// Tax-engine Fase 2: direction of the row. Defaults to <see cref="TaxChargeType.Add"/>,
    /// preserving the module-17 flat-rate behavior; <see cref="TaxChargeType.Deduct"/> rows
    /// (retentions) carry a NEGATIVE <see cref="TaxAmount"/> on normal invoices.
    /// </summary>
    public TaxChargeType ChargeType { get; set; } = TaxChargeType.Add;

    /// <summary>
    /// Tax-engine Fase 2: what the rate applies on. Defaults to
    /// <see cref="TaxCalculationBasis.OnNetTotal"/> (module-17 behavior).
    /// </summary>
    public TaxCalculationBasis CalculationBasis { get; set; } = TaxCalculationBasis.OnNetTotal;

    /// <summary>
    /// Tax-engine Fase 2: the base this row computed on (discounted net, or the previous
    /// row's total for cascades). Stored for the UI breakdown; same sign as the base.
    /// </summary>
    public decimal BaseAmount { get; set; }

    /// <summary>Server-computed tax amount (never trusted from the client).</summary>
    public decimal TaxAmount { get; set; }
}
