using Erp.Application.Features.Assets.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 10.2: the asset capitalization pipeline exercised through the CQRS handler against
/// in-memory repository doubles - scenario AS-01 verbatim ($2,400 laptop, $0 salvage, 24 months
/// → 24 × $100, status Capitalized), the exact Dr Fixed / Cr CWIP pair, the 24-line schedule,
/// the $2,400 NBV afterwards, and the guarantee that every rejected capitalization writes ZERO
/// rows (no schedule, no GL, asset untouched).
/// </summary>
public sealed class CapitalizeAssetTests
{
    private static readonly DateOnly CapitalizationDate = new(2026, 10, 1);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _fixedId = Guid.NewGuid();
    private readonly Guid _accumId = Guid.NewGuid();
    private readonly Guid _expenseId = Guid.NewGuid();
    private readonly Guid _cwipId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeItemRepository _items = new();
    private readonly FakeAssetsRepository _assets = new();

    public CapitalizeAssetTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = Guid.NewGuid(),
            Name = "Acme Industrial",
        };

        _accounts.Seed(
            Leaf(_fixedId, "1510", "Fixed Assets"),
            Leaf(_accumId, "1520", "Accumulated Depreciation"),
            Leaf(_expenseId, "5310", "Depreciation Expense"),
            Leaf(_cwipId, "1410", "CWIP"));

        _items.Seed(new Item
        {
            Id = _itemId,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            ItemCode = "LAPTOP-001",
            ItemName = "Dell Precision Laptop",
            IsActive = true,
        });
    }

    private Account Leaf(Guid id, string code, string name) =>
        new()
        {
            Id = id,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            AccountCode = code,
            AccountName = name,
            RootType = AccountRootType.Asset,
            IsGroup = false,
            IsActive = true,

        };

    private CapitalizeAssetCommandHandler Handler() => new(_companies, _accounts, _items, _assets);

    private AssetCategory SeedCategory(bool active = true, Guid? cwipAccountId = null) =>
        new()
        {
            Id = _categoryId,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            CategoryName = "IT Hardware",
            FixedAssetAccountId = _fixedId,
            AccumulatedDepreciationAccountId = _accumId,
            DepreciationExpenseAccountId = _expenseId,
            CwipAccountId = cwipAccountId ?? _cwipId,
            IsActive = active,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    /// <summary>
    /// Seeds the Draft asset the capitalization tests start from (AS-01: $2,400 laptop, $0
    /// salvage, 24 monthly depreciations from 2026-10-01). The AssetCode is unset so the handler
    /// assigns the gapless AST number inside the capitalization transaction.
    /// </summary>
    private Asset SeedAsset(
        decimal gross = 2400m,
        decimal salvage = 0m,
        AssetStatus status = AssetStatus.Draft,
        int count = 24,
        int frequency = 1)
    {
        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            AssetCode = string.Empty,
            AssetName = "Dell Precision Laptop",
            ItemId = _itemId,
            AssetCategoryId = _categoryId,
            PurchaseDate = new DateOnly(2026, 9, 15),
            AvailableForUseDate = new DateOnly(2026, 10, 1),
            GrossPurchaseAmount = gross,
            SalvageValue = salvage,
            AccumulatedDepreciation = 0m,
            DepreciationMethod = DepreciationMethod.StraightLine,
            TotalNumberOfDepreciations = count,
            FrequencyInMonths = frequency,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _assets.SeedAsset(asset);
        return asset;
    }

    private void AssertZeroWrites(Asset asset, AssetStatus expectedStatus = AssetStatus.Draft)
    {
        Assert.Empty(_assets.AddedSchedules);
        Assert.Empty(_assets.AddedGlEntries);
        Assert.Equal(expectedStatus, asset.Status);
        Assert.Equal(string.Empty, asset.AssetCode);
    }

    // ------------------------------------------------------------------ happy path (AS-01)

    /// <summary>
    /// Spec AS-01 verbatim: $2,400 / $0 salvage / 24 months → 24 × $100, status Capitalized,
    /// NBV $2,400 after capitalization (nothing depreciated yet).
    /// </summary>
    [Fact]
    public async Task Capitalize_As01Laptop_Generates24LinesOf100AndCapitalizes()
    {
        _assets.SeedCategory(SeedCategory());
        var asset = SeedAsset();

        var result = await Handler().HandleAsync(
            new CapitalizeAssetCommand(_companyId, asset.Id, CapitalizationDate),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _assets.TransactionCount); // ONE transaction (Constitution III.1/III.4)
        Assert.Equal(AssetStatus.Capitalized, asset.Status);
        Assert.Equal("AST-2026-00001", asset.AssetCode);
        Assert.Equal(2400m, asset.NetBookValue);

        var dto = result.Value!;
        Assert.Equal(AssetStatus.Capitalized, dto.Asset.Status);
        Assert.Equal("AST-2026-00001", dto.Asset.AssetCode);
        Assert.Equal(2400m, dto.Asset.NetBookValue);
        Assert.Equal(24, dto.ScheduleCount);
        Assert.Equal(2400m, dto.TotalScheduled);
        Assert.All(dto.Schedule, line => Assert.Equal(100m, line.DepreciationAmount));
        Assert.Equal(new DateOnly(2026, 11, 1), dto.Schedule[0].ScheduleDate);
        Assert.Equal(new DateOnly(2028, 10, 1), dto.Schedule[^1].ScheduleDate);
        Assert.All(dto.Schedule, line => Assert.Equal(AssetScheduleStatus.Scheduled, line.Status));

        Assert.Equal(24, _assets.AddedSchedules.Count);
        Assert.All(_assets.AddedSchedules, line => Assert.Equal(asset.Id, line.AssetId));
    }

    [Fact]
    public async Task Capitalize_As01Laptop_PostsExactDrFixedCrCwipPair()
    {
        _assets.SeedCategory(SeedCategory());
        var asset = SeedAsset();

        var result = await Handler().HandleAsync(
            new CapitalizeAssetCommand(_companyId, asset.Id, CapitalizationDate),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var gl = _assets.AddedGlEntries;
        Assert.Equal(2, gl.Count);

        var debit = Assert.Single(_assets.AddedGlEntries, l => l.AccountId == _fixedId);
        Assert.Equal(2400m, debit.Debit);
        Assert.Equal(0m, debit.Credit);
        Assert.Equal(2400m, debit.DebitInAccountCurrency);
        Assert.Equal(CapitalizationDate, debit.PostingDate);
        Assert.Equal("Asset", debit.VoucherType);
        Assert.Equal("AST-2026-00001", debit.VoucherNo);
        Assert.Equal(asset.Id, debit.VoucherId);

        var credit = Assert.Single(_assets.AddedGlEntries, l => l.AccountId == _cwipId);
        Assert.Equal(0m, credit.Debit);
        Assert.Equal(2400m, credit.Credit);
        Assert.Equal(2400m, credit.CreditInAccountCurrency);

        // Constitution III.1: the voucher balances to exactly 0.0000.
        Assert.Equal(
            _assets.AddedGlEntries.Sum(l => l.Debit),
            _assets.AddedGlEntries.Sum(l => l.Credit));
    }

    // ----------------------------------------------------------------------- rejection paths

    [Fact]
    public async Task Capitalize_UnknownAsset_FailsWithAssetNotFoundAndWritesNothing()
    {
        _assets.SeedCategory(SeedCategory());

        var result = await Handler().HandleAsync(
            new CapitalizeAssetCommand(_companyId, Guid.NewGuid(), CapitalizationDate),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.AssetNotFound, result.Error!.Code);
        Assert.Empty(_assets.AddedSchedules);
        Assert.Empty(_assets.AddedGlEntries);
    }

    [Fact]
    public async Task Capitalize_AssetOfAnotherCompany_FailsWithAssetNotFoundWithoutMutatingIt()
    {
        _assets.SeedCategory(SeedCategory());
        var asset = SeedAsset();

        var result = await Handler().HandleAsync(
            new CapitalizeAssetCommand(Guid.NewGuid(), asset.Id, CapitalizationDate),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.AssetNotFound, result.Error!.Code);
        AssertZeroWrites(asset);
    }

    [Fact]
    public async Task Capitalize_AlreadyCapitalized_FailsWithInvalidStatusTransitionAndWritesNothing()
    {
        _assets.SeedCategory(SeedCategory());
        var asset = SeedAsset(status: AssetStatus.Capitalized);
        asset.AssetCode = "AST-2026-00001";

        var result = await Handler().HandleAsync(
            new CapitalizeAssetCommand(_companyId, asset.Id, CapitalizationDate),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Empty(_assets.AddedSchedules);
        Assert.Empty(_assets.AddedGlEntries);
        Assert.Equal(AssetStatus.Capitalized, asset.Status);
    }

    [Fact]
    public async Task Capitalize_SalvageAboveGross_FailsAndWritesNothing()
    {
        _assets.SeedCategory(SeedCategory());
        var asset = SeedAsset(salvage: 2500m);

        var result = await Handler().HandleAsync(
            new CapitalizeAssetCommand(_companyId, asset.Id, CapitalizationDate),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.SalvageExceedsCost, result.Error!.Code);
        AssertZeroWrites(asset);
    }

    [Fact]
    public async Task Capitalize_InactiveCategory_FailsAndWritesNothing()
    {
        _assets.SeedCategory(SeedCategory(active: false));
        var asset = SeedAsset();

        var result = await Handler().HandleAsync(
            new CapitalizeAssetCommand(_companyId, asset.Id, CapitalizationDate),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.InactiveCategory, result.Error!.Code);
        AssertZeroWrites(asset);
    }

    [Fact]
    public async Task Capitalize_MissingCwipAccount_FailsAndWritesNothing()
    {
        var category = SeedCategory();
        category.CwipAccountId = null;
        _assets.SeedCategory(category);
        var asset = SeedAsset();

        var result = await Handler().HandleAsync(
            new CapitalizeAssetCommand(_companyId, asset.Id, CapitalizationDate),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.MissingCwipAccount, result.Error!.Code);
        AssertZeroWrites(asset);
    }

    [Fact]
    public async Task Capitalize_FrozenCapitalizationDate_FailsWithFiscalPeriodLockedAndWritesNothing()
    {
        _companies.Company!.FrozenAccountsDate = new DateOnly(2026, 12, 31);
        _assets.SeedCategory(SeedCategory());
        var asset = SeedAsset();

        var result = await Handler().HandleAsync(
            new CapitalizeAssetCommand(_companyId, asset.Id, CapitalizationDate),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, result.Error!.Code);
        AssertZeroWrites(asset);
    }

    [Fact]
    public async Task Capitalize_GroupFixedAccount_FailsWithInvalidGlAccountAndWritesNothing()
    {
        var groupFixedId = Guid.NewGuid();
        _accounts.Seed(new Account
        {
            Id = groupFixedId,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            AccountCode = "1510",
            AccountName = "Fixed Assets Group",
            IsGroup = true,
            IsActive = true,

        });

        var category = SeedCategory();
        category.FixedAssetAccountId = groupFixedId;
        _assets.SeedCategory(category);
        var asset = SeedAsset();

        var result = await Handler().HandleAsync(
            new CapitalizeAssetCommand(_companyId, asset.Id, CapitalizationDate),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.InvalidGlAccount, result.Error!.Code);
        AssertZeroWrites(asset);
    }
}
