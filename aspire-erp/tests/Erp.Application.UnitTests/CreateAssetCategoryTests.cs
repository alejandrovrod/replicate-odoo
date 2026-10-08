using Erp.Application.Features.Assets.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 10.1: the asset-category creation pipeline exercised through the CQRS handler against
/// in-memory repository doubles - the leaf-posting (Constitution III.3) acceptance on EVERY
/// linked account, the nullable gain/loss links, and the guarantee that every rejected creation
/// writes ZERO rows.
/// </summary>
public sealed class CreateAssetCategoryTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _fixedId = Guid.NewGuid();
    private readonly Guid _accumId = Guid.NewGuid();
    private readonly Guid _expenseId = Guid.NewGuid();
    private readonly Guid _cwipId = Guid.NewGuid();
    private readonly Guid _gainId = Guid.NewGuid();
    private readonly Guid _lossId = Guid.NewGuid();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeAssetsRepository _assets = new();
    private readonly Dictionary<Guid, Account> _seeded = new();

    public CreateAssetCategoryTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = Guid.NewGuid(),
            Name = "Acme Industrial",
        };

        Seed(Leaf(_fixedId, "1510", "Fixed Assets"));
        Seed(Leaf(_accumId, "1520", "Accumulated Depreciation"));
        Seed(Leaf(_expenseId, "5310", "Depreciation Expense"));
        Seed(Leaf(_cwipId, "1410", "CWIP"));
        Seed(Leaf(_gainId, "4220", "Gain on Asset Disposal"));
        Seed(Leaf(_lossId, "5320", "Loss on Asset Disposal"));
    }

    private void Seed(Account account)
    {
        _seeded[account.Id] = account;
        _accounts.Seed(account);
    }

    private Account Leaf(Guid id, string code, string name, Guid? companyId = null) =>
        new()
        {
            Id = id,
            TenantId = Guid.NewGuid(),
            CompanyId = companyId ?? _companyId,
            AccountCode = code,
            AccountName = name,
            RootType = AccountRootType.Asset,
            IsGroup = false,
            IsActive = true,

        };

    private CreateAssetCategoryCommandHandler Handler() => new(_companies, _accounts, _assets);

    private CreateAssetCategoryCommand ValidCommand() =>
        new(
            _companyId,
            "IT Hardware",
            _fixedId,
            _accumId,
            _expenseId,
            _cwipId,
            _gainId,
            _lossId);

    // ------------------------------------------------------------------ happy paths

    [Fact]
    public async Task Create_WithAllLinks_PersistsCategoryWithEveryAccount()
    {
        var result = await Handler().HandleAsync(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("IT Hardware", result.Value!.CategoryName);
        Assert.Equal(_fixedId, result.Value.FixedAssetAccountId);
        Assert.Equal(_accumId, result.Value.AccumulatedDepreciationAccountId);
        Assert.Equal(_expenseId, result.Value.DepreciationExpenseAccountId);
        Assert.Equal(_cwipId, result.Value.CwipAccountId);
        Assert.Equal(_gainId, result.Value.GainOnDisposalAccountId);
        Assert.Equal(_lossId, result.Value.LossOnDisposalAccountId);
        Assert.True(result.Value.IsActive);

        var persisted = Assert.Single(_assets.Categories);
        Assert.Equal(result.Value.Id, persisted.Id);
        Assert.Equal(_companyId, persisted.CompanyId);
        Assert.Equal("IT Hardware", persisted.CategoryName);
        Assert.Equal(_cwipId, persisted.CwipAccountId);
    }

    [Fact]
    public async Task Create_WithoutOptionalLinks_PersistsWithNulls()
    {
        var result = await Handler().HandleAsync(
            ValidCommand() with { CwipAccountId = null, GainOnDisposalAccountId = null, LossOnDisposalAccountId = null },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.CwipAccountId);
        Assert.Null(result.Value.GainOnDisposalAccountId);
        Assert.Null(result.Value.LossOnDisposalAccountId);
        Assert.Single(_assets.Categories);
    }

    // ------------------------------------------------------------------ rejection paths

    [Fact]
    public async Task Create_WithGroupFixedAccount_FailsWithInvalidGlAccountAndWritesNothing()
    {
        _seeded[_fixedId].IsGroup = true;

        var result = await Handler().HandleAsync(ValidCommand(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.InvalidGlAccount, result.Error!.Code);
        Assert.Empty(_assets.Categories);
    }

    [Fact]
    public async Task Create_WithMissingAccount_FailsWithInvalidGlAccountAndWritesNothing()
    {
        var result = await Handler().HandleAsync(
            ValidCommand() with { AccumulatedDepreciationAccountId = Guid.NewGuid() },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.InvalidGlAccount, result.Error!.Code);
        Assert.Empty(_assets.Categories);
    }

    [Fact]
    public async Task Create_WithInactiveAccount_FailsWithInvalidGlAccountAndWritesNothing()
    {
        var inactiveId = Guid.NewGuid();
        _accounts.Seed(new Account
        {
            Id = inactiveId,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            AccountCode = "5310-X",
            AccountName = "Old Expense",
            IsGroup = false,
            IsActive = false,

        });

        var result = await Handler().HandleAsync(
            ValidCommand() with { DepreciationExpenseAccountId = inactiveId },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.InvalidGlAccount, result.Error!.Code);
        Assert.Empty(_assets.Categories);
    }

    [Fact]
    public async Task Create_WithForeignCompanyAccount_FailsWithInvalidGlAccountAndWritesNothing()
    {
        var foreignId = Guid.NewGuid();
        _accounts.Seed(Leaf(foreignId, "1510", "Other Co Fixed", companyId: Guid.NewGuid()));

        var result = await Handler().HandleAsync(
            ValidCommand() with { FixedAssetAccountId = foreignId },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.InvalidGlAccount, result.Error!.Code);
        Assert.Empty(_assets.Categories);
    }

    [Fact]
    public async Task Create_WithGroupGainAccount_FailsWithInvalidGlAccountAndWritesNothing()
    {
        var groupGainId = Guid.NewGuid();
        _accounts.Seed(new Account
        {
            Id = groupGainId,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            AccountCode = "4220",
            AccountName = "Gains Group",
            IsGroup = true,
            IsActive = true,

        });

        var result = await Handler().HandleAsync(
            ValidCommand() with { GainOnDisposalAccountId = groupGainId },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.InvalidGlAccount, result.Error!.Code);
        Assert.Empty(_assets.Categories);
    }

    [Fact]
    public async Task Create_WithInactiveLossAccount_FailsWithInvalidGlAccountAndWritesNothing()
    {
        var inactiveLossId = Guid.NewGuid();
        _accounts.Seed(new Account
        {
            Id = inactiveLossId,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            AccountCode = "5320",
            AccountName = "Old Loss",
            IsGroup = false,
            IsActive = false,

        });

        var result = await Handler().HandleAsync(
            ValidCommand() with { LossOnDisposalAccountId = inactiveLossId },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.InvalidGlAccount, result.Error!.Code);
        Assert.Empty(_assets.Categories);
    }

    [Fact]
    public async Task Create_WithBlankName_FailsWithCategoryNameRequiredAndWritesNothing()
    {
        var result = await Handler().HandleAsync(
            ValidCommand() with { CategoryName = "  " },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.CategoryNameRequired, result.Error!.Code);
        Assert.Empty(_assets.Categories);
    }

    [Fact]
    public async Task Create_WithMissingCompany_FailsWithCompanyNotFoundAndWritesNothing()
    {
        _companies.Company = null;

        var result = await Handler().HandleAsync(ValidCommand(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.CompanyNotFound, result.Error!.Code);
        Assert.Empty(_assets.Categories);
    }
}
