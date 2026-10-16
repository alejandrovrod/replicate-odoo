namespace Erp.Domain.Entities;

/// <summary>
/// Stable machine-readable failure codes for the tax engine (Fase 2). They flow Domain
/// -&gt; Application (<c>Error.Code</c>) -&gt; Api, where controllers map them to RFC 7807
/// ProblemDetails - mirroring <see cref="SellingErrorCodes"/>.
/// </summary>
public static class TaxErrorCodes
{
    /// <summary>A template or invoice tax row without a GL account.</summary>
    public const string TaxAccountRequired = "tax_account_required";

    /// <summary>A tax row carries an unknown calculation basis.</summary>
    public const string InvalidTaxCalculationBasis = "invalid_tax_calculation_basis";

    /// <summary>A tax row carries an unknown charge type.</summary>
    public const string InvalidTaxChargeType = "invalid_tax_charge_type";

    /// <summary>A tax row carries an out-of-range rate (shared wire code with module 17).</summary>
    public const string InvalidTaxRate = "invalid_tax_rate";

    public const string TaxTemplateNotFound = "tax_template_not_found";

    public const string DuplicateTaxTemplate = "duplicate_tax_template";

    /// <summary>Template category must be "Sales" or "Purchase".</summary>
    public const string InvalidTaxTemplateCategory = "invalid_tax_template_category";
}
