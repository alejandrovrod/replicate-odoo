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

    // BOM must also be the default recipe for its finished item (Task 9.3 acceptance).
    public const string NonDefaultBom = "non_default_bom";

    // Referenced masters missing from this tenant (mirrors the stock/buying not-found codes).
    public const string WorkOrderNotFound = "work_order_not_found";
    public const string BomNotFound = "bom_not_found";
    public const string WorkstationNotFound = "workstation_not_found";

    // Work-order field guards (Task 9.3).
    public const string InvalidWorkOrderQuantity = "invalid_work_order_quantity";
    public const string InvalidWorkOrderDates = "invalid_work_order_dates";
    public const string InvalidWorkOrderWarehouses = "invalid_work_order_warehouses";

    // Workflow transitions (Task 9.3/9.4): any move outside the legal machine.
    public const string InvalidStatusTransition = "invalid_status_transition";

    // Manufacture of a scrap-bearing BOM cannot balance its three-leg voucher without a scrap
    // GL account (Task 9.4 boundary): rejected loudly instead of inventing accounting policy.
    public const string ScrapValuationNotSupported = "scrap_valuation_not_supported";
}
