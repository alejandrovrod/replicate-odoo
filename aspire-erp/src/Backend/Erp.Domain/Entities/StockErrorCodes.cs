namespace Erp.Domain.Entities;

/// <summary>
/// Stable machine-readable failure codes for the Stock &amp; Inventory module. They flow Domain ->
/// Application (<c>Error.Code</c>) -> Api, where the controller maps them to RFC 7807 status codes
/// (duplicate codes -> 409, everything else -> 400), mirroring AccountErrorCodes.
/// </summary>
public static class StockErrorCodes
{
    // Item / UOM
    public const string ItemCodeRequired = "item_code_required";
    public const string ItemCodeTooLong = "item_code_too_long";
    public const string ItemNameRequired = "item_name_required";
    public const string ItemNameTooLong = "item_name_too_long";
    public const string InvalidValuationMethod = "invalid_valuation_method";
    public const string BaseUomRequired = "base_uom_required";
    public const string DuplicateItemCode = "duplicate_item_code";
    public const string UomCodeRequired = "uom_code_required";
    public const string UomCodeTooLong = "uom_code_too_long";
    public const string UomNameRequired = "uom_name_required";
    public const string UomNameTooLong = "uom_name_too_long";
    public const string InvalidToBaseFactor = "invalid_to_base_factor";

    // Warehouse
    public const string WarehouseCodeRequired = "warehouse_code_required";
    public const string WarehouseCodeTooLong = "warehouse_code_too_long";
    public const string WarehouseNameRequired = "warehouse_name_required";
    public const string WarehouseNameTooLong = "warehouse_name_too_long";
    public const string DuplicateWarehouseCode = "duplicate_warehouse_code";
    public const string WarehouseCompanyRequired = "warehouse_company_required";
    public const string ParentIsSelf = "parent_is_self";
    public const string ParentNotInSameCompany = "parent_not_in_same_company";
    public const string ParentIsNotGroup = "parent_is_not_group";
    public const string CycleDetected = "cycle_detected";

    // Stock entry / posting
    public const string NoLines = "no_lines";
    public const string InvalidQuantity = "invalid_quantity";
    public const string InvalidRate = "invalid_rate";
    public const string CompanyNotFound = "company_not_found";
    public const string WarehouseNotFound = "warehouse_not_found";
    public const string ItemNotFound = "item_not_found";
    public const string ParentWarehouseNotFound = "parent_warehouse_not_found";
    public const string InvalidTargetWarehouse = "invalid_target_warehouse";
    public const string InsufficientStock = "insufficient_stock";
    public const string VoucherNotFound = "voucher_not_found";
    public const string InvalidStatusTransition = "invalid_status_transition";

    // General Ledger configuration / invariants (Constitution Article III)
    public const string MissingExpenseAccount = "missing_expense_account";
    public const string MissingStockAccount = "missing_stock_account";
    public const string StockReceivedAccountNotConfigured = "stock_received_account_not_configured";
    public const string AmbiguousAccountCode = "ambiguous_account_code";
    public const string InvalidGlAccount = "invalid_gl_account";
    public const string DoubleEntryImbalance = "double_entry_imbalance";
    public const string GlEntryAppendOnly = "gl_entry_append_only";
}
