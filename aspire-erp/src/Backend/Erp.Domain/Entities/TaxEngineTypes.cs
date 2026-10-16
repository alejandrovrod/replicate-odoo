namespace Erp.Domain.Entities;

/// <summary>
/// Direction of a tax row (tax-engine Fase 2, gap D2): a transferred tax increases the
/// payable (<c>Add</c>: IVA, IEPS), a fiscal retention decreases it (<c>Deduct</c>:
/// retención ISR, retención IVA). Persisted as the enum NAME (nvarchar), matching the
/// <see cref="PaymentType"/> precedent.
/// </summary>
public enum TaxChargeType
{
    /// <summary>Transferred/additional tax (e.g. IVA, IEPS): increases the document total.</summary>
    Add = 1,

    /// <summary>Fiscal retention (e.g. ISR/IVA withholding): decreases the document total.</summary>
    Deduct = 2,
}

/// <summary>
/// Base a tax row computes on (tax-engine Fase 2, gap D2). Persisted as the enum NAME.
/// </summary>
public enum TaxCalculationBasis
{
    /// <summary>Rate applied on the document's taxable net base (discounted net total).</summary>
    OnNetTotal = 1,

    /// <summary>
    /// Compound/cascade: rate applied on the previous row's total (its base + signed amount),
    /// e.g. IVA 16% on (Net + IEPS).
    /// </summary>
    OnPreviousRow = 2,

    /// <summary>Fixed amount (rate ignored): the row books <c>FixedAmount</c> verbatim.</summary>
    FixedAmount = 3,
}
