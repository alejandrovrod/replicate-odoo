namespace Erp.Application.Features.Security.Roles;

/// <summary>
/// Official DocType catalog (role-permission-manager spec §5): the ONLY strings a
/// permission matrix row may reference, grouped by module for the matrix editor.
/// Static by design (plan.md: "GetDocTypesCatalog es estático — va en Application,
/// no en repo") - every entry mirrors a DocType seeded by
/// <c>SecurityRoleSeeder</c>; inventing names here would orphan matrix rows.
/// </summary>
public sealed record DocTypeCatalogEntry(string DocType, string Module);

public static class DocTypeCatalog
{
    public static readonly IReadOnlyList<DocTypeCatalogEntry> All = new List<DocTypeCatalogEntry>
    {
        new("sales_order", "Sales"),
        new("sales_invoice", "Sales"),
        new("delivery_note", "Sales"),
        new("customer", "Sales"),
        new("lead", "Sales"),
        new("opportunity", "Sales"),
        new("item", "Stock"),
        new("warehouse", "Stock"),
        new("stock_entry", "Stock"),
        new("stock", "Stock"),
        new("company", "Core"),
        new("currency", "Core"),
        new("catalog", "Core"),
        new("user", "Core"),
        new("preset", "Core"),
        new("purchase_order", "Purchase"),
        new("purchase_invoice", "Purchase"),
        new("purchase_receipt", "Purchase"),
        new("supplier", "Purchase"),
        new("account", "Accounting"),
        new("journal_entry", "Accounting"),
        new("payment_entry", "Accounting"),
        new("gl_entry", "Accounting"),
        new("bank_account", "Accounting"),
        new("bank_transaction", "Accounting"),
        new("bank_statement_import", "Accounting"),
        new("bank_transaction_rule", "Accounting"),
        new("fiscal_year", "Accounting"),
        new("period_closing_voucher", "Accounting"),
        new("exchange_rate", "Accounting"),
        new("exchange_rate_revaluation", "Accounting"),
        new("asset", "Accounting"),
        new("asset_category", "Accounting"),
        new("report", "Accounting"),
        new("employee", "HR"),
        new("attendance", "HR"),
        new("leave_application", "HR"),
        new("salary_component", "HR"),
        new("salary_structure", "HR"),
        new("payroll_entry", "HR"),
        new("bom", "Manufacturing"),
        new("work_order", "Manufacturing"),
        new("project", "Manufacturing"),
    }.AsReadOnly();

    /// <summary>Case-insensitive lookup set for <c>RoleValidator.EnsurePermissionsMatrix</c>.</summary>
    public static readonly IReadOnlyCollection<string> AllDocTypes =
        All.Select(e => e.DocType).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
