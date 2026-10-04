namespace Erp.Domain.Entities;

/// <summary>
/// Stable machine-readable failure codes for the Asset Management module (Tasks 10.1-10.3). They
/// flow Domain -&gt; Application (<c>Error.Code</c>) -&gt; Api, where the controller maps them to RFC 7807
/// status codes, mirroring <see cref="StockErrorCodes"/> and
/// <see cref="ManufacturingErrorCodes"/>.
/// </summary>
/// <remarks>
/// Deliberate deviation from plan.md §2: the plan uses SCREAMING_SNAKE codes under namespace
/// <c>Erp.Domain.Assets.Errors</c>; this file follows the repo convention (snake_case codes in
/// <c>Erp.Domain.Entities</c>, exceptions in <c>Erp.Domain.Exceptions</c>) like every other
/// module.
/// </remarks>
public static class AssetErrorCodes
{
    // Asset category (Task 10.1)
    public const string CategoryNameRequired = "category_name_required";
    public const string CategoryNameTooLong = "category_name_too_long";
    public const string CategoryNotFound = "category_not_found";
    public const string InactiveCategory = "inactive_category";

    // Asset master (Task 10.2)
    public const string AssetNotFound = "asset_not_found";
    public const string AssetNameRequired = "asset_name_required";
    public const string InvalidGrossAmount = "invalid_gross_amount";
    public const string InvalidSalvageValue = "invalid_salvage_value";
    public const string SalvageExceedsCost = "salvage_exceeds_cost";
    public const string InvalidDepreciationPeriods = "invalid_depreciation_periods";
    public const string InvalidStatusTransition = "invalid_status_transition";

    // Referenced masters missing from this tenant (mirrors the stock/manufacturing not-found codes).
    public const string CompanyNotFound = "company_not_found";
    public const string ItemNotFound = "item_not_found";

    // General Ledger configuration / invariants (Constitution Article III)
    public const string InvalidGlAccount = "invalid_gl_account";
    public const string MissingCwipAccount = "missing_cwip_account";
}
