using Erp.Domain.Entities;

namespace Erp.Infrastructure.Seeders;

/// <summary>
/// Starter master data that every new tenant receives together with its chart of accounts:
/// units of measure, the root/main warehouse pair and the fixed-asset categories. Names are
/// resolved from the tenant's default language.
/// </summary>
/// <remarks>
/// Pure builder (no SaveChanges): <c>TenantOnboardingRepository</c> adds the returned rows inside
/// the same transaction that creates the tenant, company, COA and RBAC matrix.
/// </remarks>
public static class TenantStarterDataSeeder
{
    private sealed record LocalizedText(string English, string Spanish)
    {
        public string For(string language) =>
            language == TenantLanguage.English ? English : Spanish;
    }

    private sealed record UomSeed(
        LocalizedText Name,
        LocalizedText Symbol,
        bool MustBeWholeNumber);

    private sealed record WarehouseSeed(
        string Code,
        LocalizedText Name,
        bool IsGroup);

    private sealed record AssetCategorySeed(LocalizedText Name);

    private static readonly UomSeed[] Uoms =
    [
        new(new("Each", "Unit"), new("EA", "PZA"), false),
        new(new("Kilogram", "Kilogram"), new("KG", "KG"), false),
        new(new("Meter", "Meter"), new("M", "M"), false),
        new(new("Hour", "Hour"), new("HR", "HR"), false),
    ];

    private static readonly WarehouseSeed[] Warehouses =
    [
        new("WH", new("All Warehouses", "Todos los Almacenes"), true),
        new("WH-01", new("Main Stores", "Almacén Principal"), false),
    ];

    private static readonly AssetCategorySeed[] AssetCategories =
    [
        new(new("IT Hardware", "Equipo de Cómputo y TI")),
        new(new("Office Furniture", "Mobiliario y Equipo de Oficina")),
    ];

    /// <summary>
    /// Builds the starter catalogs for one tenant/company. <paramref name="accounts"/> must be the
    /// chart returned by <see cref="StandardChartOfAccounts.Build"/> for the same company.
    /// </summary>
    public static TenantStarterData Build(
        Guid tenantId,
        Guid companyId,
        IReadOnlyDictionary<string, Account> accounts,
        string language = TenantLanguage.Spanish)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        var resolvedLanguage = TenantLanguage.Resolve(language);
        var now = DateTimeOffset.UtcNow;

        var unitsOfMeasure = Uoms
            .Select(seed => new UOM
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CompanyId = companyId,
                UomName = seed.Name.For(resolvedLanguage),
                Symbol = seed.Symbol.For(resolvedLanguage),
                MustBeWholeNumber = seed.MustBeWholeNumber,
                IsActive = true,
            })
            .ToList();

        var warehouses = BuildWarehouses(tenantId, companyId, accounts, resolvedLanguage);

        var assetCategories = AssetCategories
            .Select(seed => new AssetCategory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CompanyId = companyId,
                CategoryName = seed.Name.For(resolvedLanguage),
                FixedAssetAccountId = accounts["1510"].Id,
                AccumulatedDepreciationAccountId = accounts["1520"].Id,
                DepreciationExpenseAccountId = accounts["5310"].Id,
                CwipAccountId = accounts["2120"].Id,
                GainOnDisposalAccountId = accounts["4220"].Id,
                LossOnDisposalAccountId = accounts["5320"].Id,
                IsNonDepreciable = false,
                DefaultUsefulLifeMonths = 60,
                DefaultSalvageValuePercent = 0m,
                IsActive = true,
                CreatedAt = now,
            })
            .ToList();

        return new TenantStarterData(unitsOfMeasure, warehouses, assetCategories);
    }

    private static List<Warehouse> BuildWarehouses(
        Guid tenantId,
        Guid companyId,
        IReadOnlyDictionary<string, Account> accounts,
        string language)
    {
        var rootSeed = Warehouses[0];
        var leafSeed = Warehouses[1];

        var root = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            WarehouseCode = rootSeed.Code,
            WarehouseName = rootSeed.Name.For(language),
            IsGroup = rootSeed.IsGroup,
            Type = "Physical",
            IsActive = true,
        };

        var leaf = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            WarehouseCode = leafSeed.Code,
            WarehouseName = leafSeed.Name.For(language),
            ParentWarehouseId = root.Id,
            IsGroup = leafSeed.IsGroup,
            Type = "Physical",
            AccountId = accounts["1310"].Id,
            IsActive = true,
        };

        return [root, leaf];
    }
}

/// <summary>Rows produced by <see cref="TenantStarterDataSeeder.Build"/>.</summary>
public sealed record TenantStarterData(
    IReadOnlyList<UOM> UnitsOfMeasure,
    IReadOnlyList<Warehouse> Warehouses,
    IReadOnlyList<AssetCategory> AssetCategories);
