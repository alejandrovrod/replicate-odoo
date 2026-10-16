using System;

namespace Erp.Domain.Entities;

/// <summary>
/// One row of the "Taxes and Charges" table of a purchase invoice (tax-engine Fase 2,
/// mirroring <see cref="SalesInvoiceTax"/>): transferred input taxes
/// (<see cref="TaxChargeType.Add"/>, e.g. recoverable VAT) and fiscal withholdings
/// (<see cref="TaxChargeType.Deduct"/>, e.g. ISR retention on fees/freight).
/// </summary>
/// <remarks>
/// Like <see cref="SalesInvoiceTax"/> the row carries no <c>TenantId</c> of its own -
/// tenancy rides on the parent <see cref="PurchaseInvoice"/> (Constitution Article II.1).
/// Amounts are SIGNED (see <see cref="SalesInvoiceTax"/> remarks): the document total is
/// always <c>NetTotal + Σ TaxAmount</c>.
/// </remarks>
public sealed class PurchaseInvoiceTax
{
    public Guid Id { get; set; }

    public Guid PurchaseInvoiceId { get; set; }
    public PurchaseInvoice? PurchaseInvoice { get; set; }

    /// <summary>Postable leaf account hit by this row (input-tax asset, withholding liability...).</summary>
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    /// <summary>Percentage applied (e.g. 16.00); ignored when basis is FixedAmount.</summary>
    public decimal Rate { get; set; }

    public TaxChargeType ChargeType { get; set; } = TaxChargeType.Add;

    public TaxCalculationBasis CalculationBasis { get; set; } = TaxCalculationBasis.OnNetTotal;

    /// <summary>
    /// Fixed amount booked verbatim when <see cref="CalculationBasis"/> is
    /// <see cref="TaxCalculationBasis.FixedAmount"/> (signed at computation like a rate row).
    /// </summary>
    public decimal FixedAmount { get; set; }

    /// <summary>The base this row computed on (payable net, or the previous row's total).</summary>
    public decimal BaseAmount { get; set; }

    /// <summary>Server-computed tax amount, signed (never trusted from the client).</summary>
    public decimal TaxAmount { get; set; }
}
