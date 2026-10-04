namespace Erp.Domain.Entities;

/// <summary>
/// Stable machine-readable failure codes for the Manufacturing module (Tasks 9.1-9.2). They flow
/// Domain -&gt; Application (<c>Error.Code</c>) -&gt; Api, where the controller maps them to RFC 7807
/// status codes, mirroring <see cref="StockErrorCodes"/>.
/// </summary>
/// <remarks>
/// Deliberate deviation from plan.md §2: the plan uses SCREAMING_SNAKE codes under namespace
/// <c>Erp.Domain.Manufacturing.Errors</c>; this file follows the repo convention (snake_case codes
/// in <c>Erp.Domain.Entities</c>, exceptions in <c>Erp.Domain.Exceptions</c>) like every other
/// module.
/// </remarks>
public static class ManufacturingErrorCodes
{
    // Workstation (Task 9.1)
    public const string WorkstationNameRequired = "workstation_name_required";
    public const string WorkstationNameTooLong = "workstation_name_too_long";
    public const string NegativeHourRate = "negative_hour_rate";

    // BOM header (Task 9.2)
    public const string InvalidBomQuantity = "invalid_bom_quantity";
    public const string EmptyBom = "empty_bom";
    public const string CircularReference = "circular_reference";

    // BOM item lines (Task 9.2)
    public const string InvalidBomItemQuantity = "invalid_bom_item_quantity";
    public const string NegativeValuationRate = "negative_valuation_rate";
    public const string NegativeBomAmount = "negative_bom_amount";
    public const string NegativeScrapPercentage = "negative_scrap_percentage";

    // BOM operations (Task 9.2)
    public const string InvalidOperationDuration = "invalid_operation_duration";

    // Active-BOM guard (spec MF-03; enforced by Block B work-order submission, listed here so the
    // code exists when 9.3 needs it)
    public const string InactiveBom = "inactive_bom";
}
