using Erp.Domain.Entities;

namespace Erp.Infrastructure.Seeders;

/// <summary>
/// Standard ERP chart of accounts created for every newly onboarded company
/// (Fase 5 / onboarding): the five root groups plus the posting leaves the selling, buying,
/// stock, payroll, asset and closing engines resolve at runtime.
/// </summary>
/// <remarks>
/// Pure builder (no SaveChanges): <c>TenantOnboardingRepository</c> adds the returned rows to the
/// same tracked transaction that creates the tenant/company, so a failed onboarding leaves no
/// partial COA behind. Names are resolved from the tenant's default language; codes and account
/// types stay language-independent because posting engines depend on them.
/// </remarks>
public static class StandardChartOfAccounts
{
    private sealed record Seed(
        string Code,
        string EnglishName,
        string SpanishName,
        AccountRootType RootType,
        AccountType Type,
        bool IsGroup,
        string? ParentCode);

    /// <summary>
    /// The canonical starter chart. Codes match the ones the dev seed and the posting engines
    /// already consume (1120 receivable, 2110 payable, 2120 stock received, 5210 COGS, ...).
    /// </summary>
    private static readonly Seed[] Seeds =
    [
        new("1000", "Assets", "Activos", AccountRootType.Asset, AccountType.Other, true, null),
        new("1110", "Cash and Cash Equivalents", "Efectivo y Equivalentes de Efectivo", AccountRootType.Asset, AccountType.Cash, false, "1000"),
        new("1120", "Accounts Receivable", "Cuentas por Cobrar", AccountRootType.Asset, AccountType.Receivable, false, "1000"),
        new("1130", "Input Tax Recoverable", "IVA Acreditable", AccountRootType.Asset, AccountType.Tax, false, "1000"),
        new("1310", "Stock In Hand", "Inventario en Almacén", AccountRootType.Asset, AccountType.Stock, false, "1000"),
        new("1510", "Fixed Asset Equipment", "Equipo de Activo Fijo", AccountRootType.Asset, AccountType.Other, false, "1000"),
        new("1520", "Accumulated Depreciation", "Depreciación Acumulada", AccountRootType.Asset, AccountType.Other, false, "1000"),

        new("2000", "Liabilities", "Pasivos", AccountRootType.Liability, AccountType.Other, true, null),
        new("2110", "Accounts Payable", "Cuentas por Pagar", AccountRootType.Liability, AccountType.Payable, false, "2000"),
        new("2120", "Stock Received But Not Billed", "Mercancía Recibida no Facturada", AccountRootType.Liability, AccountType.Other, false, "2000"),
        new("2150", "Payroll Payable", "Nómina por Pagar", AccountRootType.Liability, AccountType.Payable, false, "2000"),
        new("2220", "Income Tax Payable", "Impuesto sobre la Renta por Pagar", AccountRootType.Liability, AccountType.Tax, false, "2000"),

        new("3000", "Equity", "Capital Contable", AccountRootType.Equity, AccountType.Other, true, null),
        new("3100", "Retained Earnings", "Resultados Acumulados", AccountRootType.Equity, AccountType.Equity, false, "3000"),

        new("4000", "Income", "Ingresos", AccountRootType.Income, AccountType.Other, true, null),
        new("4110", "Sales Revenue", "Ingresos por Ventas", AccountRootType.Income, AccountType.Revenue, false, "4000"),
        new("4220", "Gain on Asset Disposal", "Ganancia por Disposición de Activos", AccountRootType.Income, AccountType.Revenue, false, "4000"),

        new("5000", "Expenses", "Gastos", AccountRootType.Expense, AccountType.Other, true, null),
        new("5110", "Office Supplies Expense", "Gastos de Papelería y Oficina", AccountRootType.Expense, AccountType.Expense, false, "5000"),
        new("5120", "Purchase Price Difference", "Variación en Precio de Compra", AccountRootType.Expense, AccountType.Expense, false, "5000"),
        new("5210", "Cost of Goods Sold", "Costo de Ventas", AccountRootType.Expense, AccountType.COGS, false, "5000"),
        new("5310", "Depreciation Expense", "Gasto por Depreciación", AccountRootType.Expense, AccountType.Depreciation, false, "5000"),
        new("5320", "Loss on Asset Disposal", "Pérdida por Disposición de Activos", AccountRootType.Expense, AccountType.Expense, false, "5000"),
    ];

    /// <summary>
    /// Builds the starter accounts keyed by code, with parents wired by code and names resolved
    /// from <paramref name="language"/> (<c>en</c> or <c>es</c>; anything else falls back to
    /// Spanish).
    /// </summary>
    public static IReadOnlyDictionary<string, Account> Build(
        Guid tenantId,
        Guid companyId,
        string language = TenantLanguage.Spanish)
    {
        var resolvedLanguage = TenantLanguage.Resolve(language);
        var accounts = new Dictionary<string, Account>(Seeds.Length, StringComparer.Ordinal);

        foreach (var seed in Seeds)
        {
            var account = new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CompanyId = companyId,
                AccountCode = seed.Code,
                AccountName = resolvedLanguage == TenantLanguage.English
                    ? seed.EnglishName
                    : seed.SpanishName,
                RootType = seed.RootType,
                Type = seed.Type,
                IsGroup = seed.IsGroup,
                ParentAccountId = seed.ParentCode is null ? null : accounts[seed.ParentCode].Id,
                IsActive = true,
            };

            accounts.Add(seed.Code, account);
        }

        return accounts;
    }
}
