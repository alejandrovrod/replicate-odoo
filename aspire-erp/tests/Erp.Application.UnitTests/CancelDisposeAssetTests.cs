using Erp.Application.Features.Assets.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 10.6: the asset disposal reversal pipeline exercised through the CQRS handler against
/// in-memory repository doubles - reversal of AS-03 sale and AS-05 scrap, the exact mirror GL
/// (Dr/Cr swapped), reopened schedule lines, restored AccumulatedDepreciation and status, and
/// the guarantee that every rejected reversal writes ZERO rows.
/// </summary>
public sealed class CancelDisposeAssetTests
{
    private static readonly DateOnly DisposalDate = new(2026, 11, 15);
    private static readonly DateOnly ReversalDate = new(2026, 12, 1);

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

    public CancelDisposeAssetTests()
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

    private CancelDisposeAssetCommandHandler Handler() => new(_companies, _accounts, _assets);

    private void SeedCategory() =>
        _assets.SeedCategory(new AssetCategory
        {
            Id = _categoryId,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            CategoryName = "Equipment",
            FixedAssetAccountId = _fixedId,
            AccumulatedDepreciationAccountId = _accumId,
            DepreciationExpenseAccountId = _expenseId,
            GainOnDisposalAccountId = _gainId,
            LossOnDisposalAccountId = _lossId,
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
        decimal amount,
        AssetScheduleStatus status = AssetScheduleStatus.Scheduled)
    {
        var line = new AssetDepreciationSchedule
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            ScheduleDate = date,
            DepreciationAmount = amount,
            AccumulatedDepreciationAfter = 0m,
            Status = status,
        };

        _assets.SeedSchedule(line);
        return line;
    }

    private void AssertBalanced(IReadOnlyList<GLEntry> lines)
    {
        Assert.Equal(
            lines.Sum(l => l.Debit),
            lines.Sum(l => l.Credit));
    }

    // ------------------------------------------------------------------ happy paths

    /// <summary>
    /// Reversal of AS-03 sale: $10,000 gross / $6,000 accrued, sold for $4,500 (gain $500).
    /// Reversal mirrors: Cr 1520 $6,000 + Cr 1110 $4,500 / Dr 1510 $10,000 / Dr 4220 $500.
    /// Asset back to Capitalized, AccumulatedDepreciation restored to Booked lines sum,
    /// Cancelled lines reopened, DisposalDate cleared.
    /// </summary>
    [Fact]
    public async Task ReverseDisposal_AS03Sale_MirrorsGlRestoresAssetAndReopensLines()
    {
        SeedCategory();
        var asset = SeedAsset();
        var past = SeedLine(asset, new DateOnly(2026, 10, 31), 6000m, AssetScheduleStatus.Booked);
        var future = SeedLine(asset, new DateOnly(2026, 12, 31), 100m);

        // First dispose
        var disposeHandler = new DisposeAssetCommandHandler(_companies, _accounts, _banks, _assets);
        var disposeResult = await disposeHandler.HandleAsync(
            new DisposeAssetCommand(_companyId, asset.Id, DisposalDate, 4500m, _bankAccountId),
            CancellationToken.None);

        Assert.True(disposeResult.IsSuccess);
        Assert.Equal(AssetStatus.Sold, asset.Status);
        Assert.Equal(DisposalDate, asset.DisposalDate);
        Assert.Equal(AssetScheduleStatus.Cancelled, future.Status);
        var disposalGlCount = _assets.AddedGlEntries.Count;
        Assert.Equal(4, disposalGlCount); // Accum, Fixed, Bank, Gain

        // Now reverse - reversal handler reads original GL from AddedGlEntries
        var result = await Handler().HandleAsync(
            new CancelDisposeAssetCommand(_companyId, asset.Id, ReversalDate),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _assets.TransactionCount); // dispose + reverse = 2 transactions

        var dto = result.Value!;
        Assert.Equal(AssetStatus.Capitalized, dto.Asset.Status);
        Assert.Null(dto.Asset.DisposalDate);
        Assert.Equal("RDS-2026-00001", dto.ReversalVoucherNo);
        Assert.Equal(1, dto.ReopenedLines);

        Assert.Equal(AssetStatus.Capitalized, asset.Status);
        Assert.Null(asset.DisposalDate);
        Assert.Equal(AssetScheduleStatus.Booked, past.Status); // history immutable
        Assert.Equal(AssetScheduleStatus.Scheduled, future.Status); // reopened

        // AccumulatedDepreciation restored to sum of Booked lines (6000)
        Assert.Equal(6000m, asset.AccumulatedDepreciation);

        // Total GL entries = disposal (4) + reversal (4) = 8
        var allGl = _assets.AddedGlEntries;
        Assert.Equal(8, allGl.Count);

        // Reversal entries are the last 4
        var reversalGl = allGl.Skip(disposalGlCount).ToList();
        Assert.Equal(4, reversalGl.Count);
        Assert.All(reversalGl, l =>
        {
            Assert.Equal("Asset", l.VoucherType);
            Assert.Equal("RDS-2026-00001", l.VoucherNo);
            Assert.Equal(asset.Id, l.VoucherId);
            Assert.Equal(ReversalDate, l.PostingDate);
        });

        // Mirror check: original Dr Accum(6000) -> reversal Cr Accum(6000)
        Assert.Equal(6000m, Assert.Single(reversalGl, l => l.AccountId == _accumId).Credit);
        // Original Dr Bank(4500) -> reversal Cr Bank(4500)
        Assert.Equal(4500m, Assert.Single(reversalGl, l => l.AccountId == _bankGlId).Credit);
        // Original Cr Fixed(10000) -> reversal Dr Fixed(10000)
        Assert.Equal(10000m, Assert.Single(reversalGl, l => l.AccountId == _fixedId).Debit);
        // Original Cr Gain(500) -> reversal Dr Gain(500)
        Assert.Equal(500m, Assert.Single(reversalGl, l => l.AccountId == _gainId).Debit);
        AssertBalanced(reversalGl);
    }

    /// <summary>
    /// Reversal of AS-05 scrap: $5,000 gross / $3,000 accrued, scrapped for $0 (loss $2,000).
    /// Reversal mirrors: Cr 1520 $3,000 + Cr 5320 $2,000 / Dr 1510 $5,000.
    /// Asset back to Capitalized, two futures reopened.
    /// </summary>
    [Fact]
    public async Task ReverseDisposal_AS05Scrap_MirrorsGlRestoresAssetAndReopensTwoLines()
    {
        SeedCategory();
        var asset = SeedAsset(gross: 5000m, accumulated: 3000m);
        var past = SeedLine(asset, new DateOnly(2026, 10, 31), 3000m, AssetScheduleStatus.Booked);
        var future1 = SeedLine(asset, new DateOnly(2026, 12, 31), 100m);
        var future2 = SeedLine(asset, new DateOnly(2027, 1, 31), 100m);

        var disposeHandler = new DisposeAssetCommandHandler(_companies, _accounts, _banks, _assets);
        var disposeResult = await disposeHandler.HandleAsync(
            new DisposeAssetCommand(_companyId, asset.Id, DisposalDate, 0m, null),
            CancellationToken.None);

        Assert.True(disposeResult.IsSuccess);
        Assert.Equal(AssetStatus.Scrapped, asset.Status);
        var disposalGlCount = _assets.AddedGlEntries.Count;
        Assert.Equal(3, disposalGlCount); // no bank line on scrap

        var result = await Handler().HandleAsync(
            new CancelDisposeAssetCommand(_companyId, asset.Id, ReversalDate),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var dto = result.Value!;
        Assert.Equal(AssetStatus.Capitalized, dto.Asset.Status);
        Assert.Equal(2, dto.ReopenedLines);

        Assert.Equal(AssetScheduleStatus.Scheduled, future1.Status);
        Assert.Equal(AssetScheduleStatus.Scheduled, future2.Status);
        Assert.Equal(3000m, asset.AccumulatedDepreciation);

        var allGl = _assets.AddedGlEntries;
        Assert.Equal(6, allGl.Count); // disposal (3) + reversal (3)

        var reversalGl = allGl.Skip(disposalGlCount).ToList();
        Assert.Equal(3, reversalGl.Count);
        Assert.DoesNotContain(reversalGl, l => l.AccountId == _bankGlId);

        // Mirror: original Dr Accum(3000) -> reversal Cr Accum(3000)
        Assert.Equal(3000m, Assert.Single(reversalGl, l => l.AccountId == _accumId).Credit);
        // Original Dr Loss(2000) -> reversal Cr Loss(2000)
        Assert.Equal(2000m, Assert.Single(reversalGl, l => l.AccountId == _lossId).Credit);
        // Original Cr Fixed(5000) -> reversal Dr Fixed(5000)
        Assert.Equal(5000m, Assert.Single(reversalGl, l => l.AccountId == _fixedId).Debit);
        AssertBalanced(reversalGl);
    }

    /// <summary>
    /// Reversal of break-even disposal (proceeds == NBV): no gain/loss line in original,
    /// so no gain/loss line in reversal either.
    /// </summary>
    [Fact]
    public async Task ReverseDisposal_BreakEven_NoGainLossLineInReversal()
    {
        SeedCategory();
        var asset = SeedAsset(gross: 10000m, accumulated: 6000m); // NBV = 4000
        var past = SeedLine(asset, new DateOnly(2026, 10, 31), 6000m, AssetScheduleStatus.Booked);
        var future = SeedLine(asset, new DateOnly(2026, 12, 31), 100m);

        var disposeHandler = new DisposeAssetCommandHandler(_companies, _accounts, _banks, _assets);
        var disposeResult = await disposeHandler.HandleAsync(
            new DisposeAssetCommand(_companyId, asset.Id, DisposalDate, 4000m, _bankAccountId),
            CancellationToken.None);

        Assert.True(disposeResult.IsSuccess);
        var disposalGlCount = _assets.AddedGlEntries.Count;
        Assert.Equal(3, disposalGlCount); // Accum, Fixed, Bank only

        var result = await Handler().HandleAsync(
            new CancelDisposeAssetCommand(_companyId, asset.Id, ReversalDate),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var allGl = _assets.AddedGlEntries;
        Assert.Equal(6, allGl.Count); // disposal (3) + reversal (3)

        var reversalGl = allGl.Skip(disposalGlCount).ToList();
        Assert.Equal(3, reversalGl.Count);
        Assert.DoesNotContain(reversalGl, l => l.AccountId == _gainId);
        Assert.DoesNotContain(reversalGl, l => l.AccountId == _lossId);
        AssertBalanced(reversalGl);
    }

    /// <summary>
    /// Reversal restores FullyDepreciated when no Scheduled lines remain and accumulated == base.
    /// </summary>
    [Fact]
    public async Task ReverseDisposal_RestoresFullyDepreciatedWhenNoScheduledLinesRemain()
    {
        SeedCategory();
        var asset = SeedAsset(gross: 2400m, accumulated: 2400m); // fully depreciated
        var past = SeedLine(asset, new DateOnly(2026, 10, 31), 2400m, AssetScheduleStatus.Booked);
        // No future lines

        var disposeHandler = new DisposeAssetCommandHandler(_companies, _accounts, _banks, _assets);
        var disposeResult = await disposeHandler.HandleAsync(
            new DisposeAssetCommand(_companyId, asset.Id, DisposalDate, 0m, null),
            CancellationToken.None);

        Assert.True(disposeResult.IsSuccess);
        var disposalGlCount = _assets.AddedGlEntries.Count;

        var result = await Handler().HandleAsync(
            new CancelDisposeAssetCommand(_companyId, asset.Id, ReversalDate),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetStatus.FullyDepreciated, asset.Status);
        Assert.Equal(2400m, asset.AccumulatedDepreciation);
    }

    // ----------------------------------------------------------------------- rejection paths

    /// <summary>Capitalized asset cannot be reversed (asset_not_disposed), zero writes.</summary>
    [Fact]
    public async Task ReverseDisposal_CapitalizedAsset_FailsWithAssetNotDisposedAndZeroWrites()
    {
        SeedCategory();
        var asset = SeedAsset(status: AssetStatus.Capitalized);
        var future = SeedLine(asset, new DateOnly(2026, 12, 31), 100m);

        var result = await Handler().HandleAsync(
            new CancelDisposeAssetCommand(_companyId, asset.Id, ReversalDate),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.AssetNotDisposed, result.Error!.Code);
        Assert.Empty(_assets.AddedGlEntries);
        Assert.Equal(AssetStatus.Capitalized, asset.Status);
        Assert.Equal(AssetScheduleStatus.Scheduled, future.Status);
    }

    /// <summary>Draft asset cannot be reversed (asset_not_disposed), zero writes.</summary>
    [Fact]
    public async Task ReverseDisposal_DraftAsset_FailsWithAssetNotDisposedAndZeroWrites()
    {
        SeedCategory();
        var asset = SeedAsset(status: AssetStatus.Draft);

        var result = await Handler().HandleAsync(
            new CancelDisposeAssetCommand(_companyId, asset.Id, ReversalDate),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.AssetNotDisposed, result.Error!.Code);
        Assert.Empty(_assets.AddedGlEntries);
        Assert.Equal(AssetStatus.Draft, asset.Status);
    }

    /// <summary>Already reversed asset (DisposalDate == null) fails (already_reversed), zero writes.</summary>
    [Fact]
    public async Task ReverseDisposal_AlreadyReversed_FailsWithAlreadyReversedAndZeroWrites()
    {
        SeedCategory();
        var asset = SeedAsset();
        var future = SeedLine(asset, new DateOnly(2026, 12, 31), 100m);

        var disposeHandler = new DisposeAssetCommandHandler(_companies, _accounts, _banks, _assets);
        await disposeHandler.HandleAsync(
            new DisposeAssetCommand(_companyId, asset.Id, DisposalDate, 4500m, _bankAccountId),
            CancellationToken.None);

        // First reversal
        var first = await Handler().HandleAsync(
            new CancelDisposeAssetCommand(_companyId, asset.Id, ReversalDate),
            CancellationToken.None);
        Assert.True(first.IsSuccess);
        var glCountAfterFirst = _assets.AddedGlEntries.Count;

        // Second reversal attempt
        var second = await Handler().HandleAsync(
            new CancelDisposeAssetCommand(_companyId, asset.Id, ReversalDate),
            CancellationToken.None);

        Assert.False(second.IsSuccess);
        Assert.Equal(AssetErrorCodes.AlreadyReversed, second.Error!.Code);
        Assert.Equal(glCountAfterFirst, _assets.AddedGlEntries.Count); // zero new rows
        Assert.Equal(AssetStatus.Capitalized, asset.Status);
        Assert.Null(asset.DisposalDate);
    }

    /// <summary>Frozen reversal date fails (fiscal_period_locked), zero writes.</summary>
    [Fact]
    public async Task ReverseDisposal_FrozenDate_FailsWithFiscalPeriodLockedAndZeroWrites()
    {
        SeedCategory();
        var asset = SeedAsset();
        var future = SeedLine(asset, new DateOnly(2026, 12, 31), 100m);

        var disposeHandler = new DisposeAssetCommandHandler(_companies, _accounts, _banks, _assets);
        await disposeHandler.HandleAsync(
            new DisposeAssetCommand(_companyId, asset.Id, DisposalDate, 4500m, _bankAccountId),
            CancellationToken.None);

        // Now freeze the period for the reversal date (2026-12-01)
        _companies.Company!.FrozenAccountsDate = new DateOnly(2026, 12, 31);
        var glCountBefore = _assets.AddedGlEntries.Count;

        var result = await Handler().HandleAsync(
            new CancelDisposeAssetCommand(_companyId, asset.Id, ReversalDate),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, result.Error!.Code);
        Assert.Equal(glCountBefore, _assets.AddedGlEntries.Count); // zero new rows
        Assert.Equal(AssetStatus.Sold, asset.Status);
        Assert.Equal(DisposalDate, asset.DisposalDate);
        Assert.Equal(AssetScheduleStatus.Cancelled, future.Status);
    }

    /// <summary>RowVersion race fails (concurrency_conflict), zero writes.</summary>
    [Fact]
    public async Task ReverseDisposal_StaleRowVersion_FailsWithConflictAndZeroWrites()
    {
        SeedCategory();
        var asset = SeedAsset();
        var future = SeedLine(asset, new DateOnly(2026, 12, 31), 100m);

        var disposeHandler = new DisposeAssetCommandHandler(_companies, _accounts, _banks, _assets);
        await disposeHandler.HandleAsync(
            new DisposeAssetCommand(_companyId, asset.Id, DisposalDate, 4500m, _bankAccountId),
            CancellationToken.None);

        var glCountBefore = _assets.AddedGlEntries.Count;
        _assets.FailNextAssetUpdate = true;

        var result = await Handler().HandleAsync(
            new CancelDisposeAssetCommand(_companyId, asset.Id, ReversalDate, new byte[] { 1, 2, 3, 4 }),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
        Assert.Equal(glCountBefore, _assets.AddedGlEntries.Count); // zero new rows
        Assert.Equal(AssetStatus.Sold, asset.Status);
        Assert.Equal(DisposalDate, asset.DisposalDate);
        Assert.Equal(AssetScheduleStatus.Cancelled, future.Status);
    }

    /// <summary>Asset not found fails, zero writes.</summary>
    [Fact]
    public async Task ReverseDisposal_UnknownAsset_FailsWithAssetNotFoundAndZeroWrites()
    {
        SeedCategory();

        var result = await Handler().HandleAsync(
            new CancelDisposeAssetCommand(_companyId, Guid.NewGuid(), ReversalDate),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.AssetNotFound, result.Error!.Code);
        Assert.Empty(_assets.AddedGlEntries);
    }

    /// <summary>Asset of another company fails with not found (no cross-company leak), zero writes.</summary>
    [Fact]
    public async Task ReverseDisposal_AssetOfAnotherCompany_FailsWithAssetNotFoundAndZeroWrites()
    {
        SeedCategory();
        var asset = SeedAsset();

        var result = await Handler().HandleAsync(
            new CancelDisposeAssetCommand(Guid.NewGuid(), asset.Id, ReversalDate),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.AssetNotFound, result.Error!.Code);
        Assert.Empty(_assets.AddedGlEntries);
    }
}