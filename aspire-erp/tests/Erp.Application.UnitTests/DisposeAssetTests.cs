using Erp.Application.Features.Assets.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 10.5: the asset disposal pipeline exercised through the CQRS handler against in-memory
/// repository doubles - scenario AS-03 verbatim (Dr 1110 $4,500 + Dr 1520 $6,000 / Cr 1510
/// $10,000 / Cr 4220 $500 → Sold), scenario AS-05 verbatim ($3,000 + $2,000 / $5,000 → Scrapped
/// with futures Cancelled), the break-even shape (no gain/loss line), and the guarantee that
/// every rejected disposal writes ZERO rows.
/// </summary>
public sealed class DisposeAssetTests
{
    private static readonly DateOnly DisposalDate = new(2026, 11, 15);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _fixedId = Guid.NewGuid();
    private readonly Guid _accumId = Guid.NewGuid();
    private readonly Guid _expenseId = Guid.NewGuid();
    private readonly Guid _gainId = Guid.NewGuid();
    private readonly Guid _lossId = Guid.NewGuid();
    private readonly Guid _bankGlId = Guid.NewGuid();
    private readonly Guid _bankAccountId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeBankRepository _banks = new();
    private readonly FakeAssetsRepository _assets = new();

    public DisposeAssetTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = Guid.NewGuid(),
            Name = "Acme Industrial",
        };

        _accounts.Seed(
            Leaf(_fixedId, "1510", "Fixed Asset Equipment"),
            Leaf(_accumId, "1520", "Accumulated Depreciation"),
            Leaf(_expenseId, "5310", "Depreciation Expense"),
            Leaf(_gainId, "4220", "Gain on Asset Disposal"),
            Leaf(_lossId, "5320", "Loss on Asset Disposal"),
            Leaf(_bankGlId, "1110", "Cash and Cash Equivalents"));

        _banks.SeedAccount(new BankAccount
        {
            Id = _bankAccountId,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            AccountName = "Main Operating",
            BankName = "First Bank",
            AccountNumber = "000123",
            Currency = "USD",
            GLAccountId = _bankGlId,
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
            Currency = "USD",
        };

    private DisposeAssetCommandHandler Handler() => new(_companies, _accounts, _banks, _assets);

    private void SeedCategory(bool withGainLoss = true) =>
        _assets.SeedCategory(new AssetCategory
        {
            Id = _categoryId,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            CategoryName = "Equipment",
            FixedAssetAccountId = _fixedId,
            AccumulatedDepreciationAccountId = _accumId,
            DepreciationExpenseAccountId = _expenseId,
            GainOnDisposalAccountId = withGainLoss ? _gainId : null,
            LossOnDisposalAccountId = withGainLoss ? _lossId : null,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });

    private Asset SeedAsset(
        decimal gross = 10000m,
        decimal accumulated = 6000m,
        AssetStatus status = AssetStatus.Capitalized,
        string code = "AST-2026-00001")
    {
        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            AssetCode = code,
            AssetName = "CNC Mill",
            ItemId = Guid.NewGuid(),
            AssetCategoryId = _categoryId,
            PurchaseDate = new DateOnly(2025, 1, 10),
            AvailableForUseDate = new DateOnly(2025, 2, 1),
            GrossPurchaseAmount = gross,
            SalvageValue = 0m,
            AccumulatedDepreciation = accumulated,
            DepreciationMethod = DepreciationMethod.StraightLine,
            TotalNumberOfDepreciations = 60,
            FrequencyInMonths = 1,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _assets.SeedAsset(asset);
        return asset;
    }

    private AssetDepreciationSchedule SeedLine(
        Asset asset,
        DateOnly date,
        AssetScheduleStatus status = AssetScheduleStatus.Scheduled)
    {
        var line = new AssetDepreciationSchedule
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            ScheduleDate = date,
            DepreciationAmount = 100m,
            AccumulatedDepreciationAfter = 0m,
            Status = status,
        };

        _assets.SeedSchedule(line);
        return line;
    }

    private void AssertBalanced()
    {
        Assert.Equal(
            _assets.AddedGlEntries.Sum(l => l.Debit),
            _assets.AddedGlEntries.Sum(l => l.Credit));
    }

    // ------------------------------------------------------------------ happy paths (AS-03)

    /// <summary>
    /// Spec AS-03 verbatim: $10,000 gross / $6,000 accrued, sold for $4,500 → Dr 1110 $4,500 +
    /// Dr 1520 $6,000 / Cr 1510 $10,000 / Cr 4220 $500 (gain), status Sold.
    /// </summary>
    [Fact]
    public async Task Dispose_AS03Sale_BooksExactFourLegsAndSolds()
    {
        SeedCategory();
        var asset = SeedAsset();
        var past = SeedLine(asset, new DateOnly(2026, 10, 31), AssetScheduleStatus.Booked);
        var future = SeedLine(asset, new DateOnly(2026, 12, 31));

        var result = await Handler().HandleAsync(
            new DisposeAssetCommand(_companyId, asset.Id, DisposalDate, 4500m, _bankAccountId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _assets.TransactionCount); // ONE transaction (Constitution III.1)

        var dto = result.Value!;
        Assert.Equal(AssetStatus.Sold, dto.Asset.Status);
        Assert.Equal(DisposalDate, dto.Asset.DisposalDate);
        Assert.Equal("DSP-2026-00001", dto.VoucherNo);
        Assert.Equal(4500m, dto.ProceedsAmount);
        Assert.Equal(500m, dto.NetGainLoss);
        Assert.Equal(1, dto.CancelledFutureLines);

        Assert.Equal(AssetStatus.Sold, asset.Status);
        Assert.Equal(DisposalDate, asset.DisposalDate);
        Assert.Equal(AssetScheduleStatus.Booked, past.Status); // history immutable
        Assert.Equal(AssetScheduleStatus.Cancelled, future.Status);

        var gl = _assets.AddedGlEntries;
        Assert.Equal(4, gl.Count);
        Assert.All(gl, l =>
        {
            Assert.Equal("Asset", l.VoucherType);
            Assert.Equal("DSP-2026-00001", l.VoucherNo);
            Assert.Equal(asset.Id, l.VoucherId);
            Assert.Equal(DisposalDate, l.PostingDate);
        });

        Assert.Equal(4500m, Assert.Single(gl, l => l.AccountId == _bankGlId).Debit);
        Assert.Equal(6000m, Assert.Single(gl, l => l.AccountId == _accumId).Debit);
        Assert.Equal(10000m, Assert.Single(gl, l => l.AccountId == _fixedId).Credit);
        Assert.Equal(500m, Assert.Single(gl, l => l.AccountId == _gainId).Credit);
        AssertBalanced();
    }

    /// <summary>
    /// Spec AS-05 verbatim: $5,000 gross / $3,000 accrued, scrapped for $0 → Dr 1520 $3,000 +
    /// Dr 5320 $2,000 / Cr 1510 $5,000 (no bank line), status Scrapped, futures Cancelled.
    /// </summary>
    [Fact]
    public async Task Dispose_AS05Scrap_BooksLossCancelsFuturesAndScraps()
    {
        SeedCategory();
        var asset = SeedAsset(gross: 5000m, accumulated: 3000m);
        var future1 = SeedLine(asset, new DateOnly(2026, 12, 31));
        var future2 = SeedLine(asset, new DateOnly(2027, 1, 31));

        var result = await Handler().HandleAsync(
            new DisposeAssetCommand(_companyId, asset.Id, DisposalDate, 0m, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var dto = result.Value!;
        Assert.Equal(AssetStatus.Scrapped, dto.Asset.Status);
        Assert.Equal("DSP-2026-00001", dto.VoucherNo);
        Assert.Equal(0m, dto.ProceedsAmount);
        Assert.Equal(-2000m, dto.NetGainLoss);
        Assert.Equal(2, dto.CancelledFutureLines);

        Assert.Equal(AssetScheduleStatus.Cancelled, future1.Status);
        Assert.Equal(AssetScheduleStatus.Cancelled, future2.Status);

        var gl = _assets.AddedGlEntries;
        Assert.Equal(3, gl.Count); // no bank line on a scrap
        Assert.DoesNotContain(gl, l => l.AccountId == _bankGlId);
        Assert.Equal(3000m, Assert.Single(gl, l => l.AccountId == _accumId).Debit);
        Assert.Equal(2000m, Assert.Single(gl, l => l.AccountId == _lossId).Debit);
        Assert.Equal(5000m, Assert.Single(gl, l => l.AccountId == _fixedId).Credit);
        AssertBalanced();
    }

    /// <summary>
    /// Break-even (proceeds == NBV): neither a gain nor a loss line is emitted, and the
    /// gain/loss links are not even required.
    /// </summary>
    [Fact]
    public async Task Dispose_BreakEven_EmitsNoGainLossLine()
    {
        SeedCategory(withGainLoss: false);
        var asset = SeedAsset(gross: 10000m, accumulated: 6000m);

        var result = await Handler().HandleAsync(
            new DisposeAssetCommand(_companyId, asset.Id, DisposalDate, 4000m, _bankAccountId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var dto = result.Value!;
        Assert.Equal(AssetStatus.Sold, dto.Asset.Status);
        Assert.Equal(0m, dto.NetGainLoss);

        var gl = _assets.AddedGlEntries;
        Assert.Equal(3, gl.Count);
        Assert.DoesNotContain(gl, l => l.AccountId == _gainId);
        Assert.DoesNotContain(gl, l => l.AccountId == _lossId);
        AssertBalanced();
    }

    // ----------------------------------------------------------------------- rejection paths

    /// <summary>Gain without a gain link fails typed (missing_gain_loss_account), zero writes.</summary>
    [Fact]
    public async Task Dispose_MissingGainAccount_FailsWithTypedCodeAndZeroWrites()
    {
        SeedCategory(withGainLoss: false);
        var asset = SeedAsset();
        var future = SeedLine(asset, new DateOnly(2026, 12, 31));

        var result = await Handler().HandleAsync(
            new DisposeAssetCommand(_companyId, asset.Id, DisposalDate, 4500m, _bankAccountId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.MissingGainLossAccount, result.Error!.Code);
        Assert.Empty(_assets.AddedGlEntries);
        Assert.Equal(AssetStatus.Capitalized, asset.Status);
        Assert.Null(asset.DisposalDate);
        Assert.Equal(AssetScheduleStatus.Scheduled, future.Status);
    }

    /// <summary>Loss without a loss link fails typed (missing_gain_loss_account), zero writes.</summary>
    [Fact]
    public async Task Dispose_MissingLossAccount_FailsWithTypedCodeAndZeroWrites()
    {
        SeedCategory(withGainLoss: false);
        var asset = SeedAsset(gross: 5000m, accumulated: 3000m);

        var result = await Handler().HandleAsync(
            new DisposeAssetCommand(_companyId, asset.Id, DisposalDate, 0m, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.MissingGainLossAccount, result.Error!.Code);
        Assert.Empty(_assets.AddedGlEntries);
        Assert.Equal(AssetStatus.Capitalized, asset.Status);
    }

    /// <summary>A Draft asset cannot be disposed (invalid_status_transition → 409).</summary>
    [Fact]
    public async Task Dispose_DraftAsset_FailsWithInvalidStatusTransitionAndZeroWrites()
    {
        SeedCategory();
        var asset = SeedAsset(status: AssetStatus.Draft);

        var result = await Handler().HandleAsync(
            new DisposeAssetCommand(_companyId, asset.Id, DisposalDate, 0m, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Empty(_assets.AddedGlEntries);
        Assert.Equal(AssetStatus.Draft, asset.Status);
    }

    /// <summary>Double disposal: disposing an already-Sold asset fails, books nothing.</summary>
    [Fact]
    public async Task Dispose_DoubleDisposal_FailsWithInvalidStatusTransition()
    {
        SeedCategory();
        var asset = SeedAsset();

        var first = await Handler().HandleAsync(
            new DisposeAssetCommand(_companyId, asset.Id, DisposalDate, 4500m, _bankAccountId),
            CancellationToken.None);
        Assert.True(first.IsSuccess);
        var glCount = _assets.AddedGlEntries.Count;

        var second = await Handler().HandleAsync(
            new DisposeAssetCommand(_companyId, asset.Id, DisposalDate, 4500m, _bankAccountId),
            CancellationToken.None);

        Assert.False(second.IsSuccess);
        Assert.Equal(AssetErrorCodes.InvalidStatusTransition, second.Error!.Code);
        Assert.Equal(glCount, _assets.AddedGlEntries.Count); // zero new rows
        Assert.Equal(AssetStatus.Sold, asset.Status);
    }

    /// <summary>Proceeds without a bank (would-be credit sale) are rejected (400-shape).</summary>
    [Fact]
    public async Task Dispose_ProceedsWithoutBank_FailsWithInvalidProceeds()
    {
        SeedCategory();
        var asset = SeedAsset();

        var result = await Handler().HandleAsync(
            new DisposeAssetCommand(_companyId, asset.Id, DisposalDate, 4500m, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.InvalidProceeds, result.Error!.Code);
        Assert.Empty(_assets.AddedGlEntries);
        Assert.Equal(AssetStatus.Capitalized, asset.Status);
    }

    /// <summary>A bank attached to a $0 scrap is a mixed shape: rejected (400-shape).</summary>
    [Fact]
    public async Task Dispose_BankWithoutProceeds_FailsWithInvalidProceeds()
    {
        SeedCategory();
        var asset = SeedAsset(gross: 5000m, accumulated: 3000m);

        var result = await Handler().HandleAsync(
            new DisposeAssetCommand(_companyId, asset.Id, DisposalDate, 0m, _bankAccountId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.InvalidProceeds, result.Error!.Code);
        Assert.Empty(_assets.AddedGlEntries);
        Assert.Equal(AssetStatus.Capitalized, asset.Status);
    }

    /// <summary>Frozen disposal date fails the whole disposal (fiscal_period_locked → 409).</summary>
    [Fact]
    public async Task Dispose_FrozenDate_FailsWithFiscalPeriodLockedAndZeroWrites()
    {
        _companies.Company!.FrozenAccountsDate = new DateOnly(2026, 12, 31);
        SeedCategory();
        var asset = SeedAsset();
        var future = SeedLine(asset, new DateOnly(2026, 12, 31));

        var result = await Handler().HandleAsync(
            new DisposeAssetCommand(_companyId, asset.Id, DisposalDate, 4500m, _bankAccountId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, result.Error!.Code);
        Assert.Empty(_assets.AddedGlEntries);
        Assert.Equal(AssetStatus.Capitalized, asset.Status);
        Assert.Null(asset.DisposalDate);
        Assert.Equal(AssetScheduleStatus.Scheduled, future.Status);
    }

    /// <summary>
    /// Spec AS-06 groundwork: a RowVersion race between load and save aborts the disposal with
    /// zero writes (no status flip, no cancellations, no GL).
    /// </summary>
    [Fact]
    public async Task Dispose_StaleRowVersion_FailsWithConflictAndZeroWrites()
    {
        SeedCategory();
        var asset = SeedAsset();
        var future = SeedLine(asset, new DateOnly(2026, 12, 31));
        _assets.FailNextAssetUpdate = true;

        var result = await Handler().HandleAsync(
            new DisposeAssetCommand(_companyId, asset.Id, DisposalDate, 4500m, _bankAccountId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
        Assert.Empty(_assets.AddedGlEntries);
        Assert.Equal(AssetStatus.Capitalized, asset.Status);
        Assert.Null(asset.DisposalDate);
        Assert.Equal(AssetScheduleStatus.Scheduled, future.Status);
    }
}
