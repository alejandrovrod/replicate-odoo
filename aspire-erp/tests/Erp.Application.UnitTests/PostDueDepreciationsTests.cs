using Erp.Application.Features.Assets.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 10.4: the periodic depreciation run exercised through the CQRS handler against
/// in-memory repository doubles - scenario AS-02 verbatim ($100 Dr 5310 / Cr 1520, NBV
/// $2,400 → $2,300), the one-voucher-per-run batch, the AS-04 replay skip (Booked rows carry
/// their identity, zero duplicate GL), the frozen-date whole-run abort, the RowVersion race
/// abort and the guarantee that every rejected run writes ZERO rows.
/// </summary>
public sealed class PostDueDepreciationsTests
{
    private static readonly DateOnly AsOf = new(2026, 10, 31);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _fixedId = Guid.NewGuid();
    private readonly Guid _accumId = Guid.NewGuid();
    private readonly Guid _expenseId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeAssetsRepository _assets = new();

    public PostDueDepreciationsTests()
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
            Leaf(_expenseId, "5310", "Depreciation Expense"));
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

    private PostDueDepreciationsCommandHandler Handler() => new(_companies, _accounts, _assets);

    private void SeedCategory() =>
        _assets.SeedCategory(new AssetCategory
        {
            Id = _categoryId,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            CategoryName = "IT Hardware",
            FixedAssetAccountId = _fixedId,
            AccumulatedDepreciationAccountId = _accumId,
            DepreciationExpenseAccountId = _expenseId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });

    private Asset SeedAsset(
        string code = "AST-2026-00001",
        decimal gross = 2400m,
        decimal salvage = 0m,
        decimal accumulated = 0m,
        AssetStatus status = AssetStatus.Capitalized)
    {
        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            AssetCode = code,
            AssetName = "Dell Precision Laptop",
            ItemId = Guid.NewGuid(),
            AssetCategoryId = _categoryId,
            PurchaseDate = new DateOnly(2026, 9, 15),
            AvailableForUseDate = new DateOnly(2026, 10, 1),
            GrossPurchaseAmount = gross,
            SalvageValue = salvage,
            AccumulatedDepreciation = accumulated,
            DepreciationMethod = DepreciationMethod.StraightLine,
            TotalNumberOfDepreciations = 24,
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

    // ------------------------------------------------------------------ happy paths (AS-02)

    /// <summary>
    /// Spec AS-02 verbatim: one $100 line due → Dr 5310 $100 / Cr 1520 $100, NBV $2,400 → $2,300,
    /// line Booked, single DEP voucher.
    /// </summary>
    [Fact]
    public async Task Run_AS02SingleLine_BooksExactDrExpenseCrAccum()
    {
        SeedCategory();
        var asset = SeedAsset();
        var line = SeedLine(asset, new DateOnly(2026, 10, 31), 100m);

        var result = await Handler().HandleAsync(
            new PostDueDepreciationsCommand(_companyId, AsOf), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _assets.TransactionCount); // ONE transaction (Constitution III.1)

        var dto = result.Value!;
        Assert.Equal(1, dto.BookedCount);
        Assert.Equal(100m, dto.TotalBooked);
        Assert.Equal("DEP-2026-00001", dto.VoucherNo);
        Assert.Equal(0, dto.SkippedCount);

        Assert.Equal(AssetScheduleStatus.Booked, line.Status);
        Assert.Equal(100m, asset.AccumulatedDepreciation);
        Assert.Equal(2300m, asset.NetBookValue);
        Assert.Equal(AssetStatus.Capitalized, asset.Status); // 23 lines still Scheduled

        var gl = _assets.AddedGlEntries;
        Assert.Equal(2, gl.Count);

        var debit = Assert.Single(gl, l => l.AccountId == _expenseId);
        Assert.Equal(100m, debit.Debit);
        Assert.Equal(0m, debit.Credit);
        Assert.Equal(new DateOnly(2026, 10, 31), debit.PostingDate);
        Assert.Equal("Asset", debit.VoucherType);
        Assert.Equal("DEP-2026-00001", debit.VoucherNo);
        Assert.Equal(line.Id, debit.VoucherId);

        var credit = Assert.Single(gl, l => l.AccountId == _accumId);
        Assert.Equal(0m, credit.Debit);
        Assert.Equal(100m, credit.Credit);
        Assert.Equal("DEP-2026-00001", credit.VoucherNo);

        Assert.Equal(gl.Sum(l => l.Debit), gl.Sum(l => l.Credit));
    }

    /// <summary>
    /// Two assets, several due lines: one batch voucher, per-asset NBV math, and the
    /// FullyDepreciated transition exactly when the last Scheduled line books.
    /// </summary>
    [Fact]
    public async Task Run_MultiAssetMultiLine_BooksBatchAndTransitionsFullyDepreciated()
    {
        SeedCategory();
        var finishing = SeedAsset("AST-2026-00001", gross: 200m);
        var lineA1 = SeedLine(finishing, new DateOnly(2026, 9, 30), 100m);
        var lineA2 = SeedLine(finishing, new DateOnly(2026, 10, 31), 100m);

        var ongoing = SeedAsset("AST-2026-00002", gross: 300m);
        var lineB1 = SeedLine(ongoing, new DateOnly(2026, 9, 30), 100m);
        var lineB2 = SeedLine(ongoing, new DateOnly(2026, 10, 31), 100m);
        var future = SeedLine(ongoing, new DateOnly(2026, 11, 30), 100m);

        var result = await Handler().HandleAsync(
            new PostDueDepreciationsCommand(_companyId, AsOf), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var dto = result.Value!;
        Assert.Equal(4, dto.BookedCount);
        Assert.Equal(400m, dto.TotalBooked);
        Assert.Equal("DEP-2026-00001", dto.VoucherNo);
        Assert.Equal(0, dto.SkippedCount);

        // One voucher for the whole run: 4 lines x Dr/Cr = 8 GL rows sharing the number.
        Assert.Equal(8, _assets.AddedGlEntries.Count);
        Assert.All(_assets.AddedGlEntries, l => Assert.Equal("DEP-2026-00001", l.VoucherNo));

        Assert.Equal(AssetScheduleStatus.Booked, lineA1.Status);
        Assert.Equal(AssetScheduleStatus.Booked, lineA2.Status);
        Assert.Equal(AssetScheduleStatus.Booked, lineB1.Status);
        Assert.Equal(AssetScheduleStatus.Booked, lineB2.Status);
        Assert.Equal(AssetScheduleStatus.Scheduled, future.Status); // future untouched

        Assert.Equal(200m, finishing.AccumulatedDepreciation);
        Assert.Equal(AssetStatus.FullyDepreciated, finishing.Status); // nothing Scheduled left

        Assert.Equal(200m, ongoing.AccumulatedDepreciation);
        Assert.Equal(100m, ongoing.NetBookValue);
        Assert.Equal(AssetStatus.Capitalized, ongoing.Status); // one line still Scheduled
    }

    // ------------------------------------------------------------- replay safety (AS-04)

    /// <summary>
    /// Spec AS-04: Booked and Cancelled rows are skipped WITH their identity and reason; a rerun
    /// books nothing new (zero duplicate GLEntry rows).
    /// </summary>
    [Fact]
    public async Task Run_BookedAndCancelledLines_SkippedWithIdentityAndNoDuplicates()
    {
        SeedCategory();
        var asset = SeedAsset(accumulated: 100m);
        var booked = SeedLine(asset, new DateOnly(2026, 9, 30), 100m, AssetScheduleStatus.Booked);
        var cancelled = SeedLine(asset, new DateOnly(2026, 10, 15), 100m, AssetScheduleStatus.Cancelled);
        var due = SeedLine(asset, new DateOnly(2026, 10, 31), 100m);

        var first = await Handler().HandleAsync(
            new PostDueDepreciationsCommand(_companyId, AsOf), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(1, first.Value!.BookedCount);
        Assert.Equal(2, first.Value!.SkippedCount);
        Assert.Contains(first.Value!.Skipped,
            s => s.ScheduleLineId == booked.Id && s.AssetId == asset.Id && s.Reason == "already_booked");
        Assert.Contains(first.Value!.Skipped,
            s => s.ScheduleLineId == cancelled.Id && s.AssetId == asset.Id && s.Reason == "line_cancelled");
        Assert.Equal(2, _assets.AddedGlEntries.Count);

        // Replay: everything is Booked/Cancelled now - zero new GL rows.
        var replay = await Handler().HandleAsync(
            new PostDueDepreciationsCommand(_companyId, AsOf), CancellationToken.None);

        Assert.True(replay.IsSuccess);
        Assert.Equal(0, replay.Value!.BookedCount);
        Assert.Equal(3, replay.Value!.SkippedCount);
        Assert.Contains(replay.Value!.Skipped,
            s => s.ScheduleLineId == due.Id && s.Reason == "already_booked");
        Assert.Equal(2, _assets.AddedGlEntries.Count); // no duplicates
        Assert.Equal(200m, asset.AccumulatedDepreciation); // no double expense
    }

    /// <summary>Lines after AsOfDate are never touched: no booking, no GL, status intact.</summary>
    [Fact]
    public async Task Run_FutureLines_Untouched()
    {
        SeedCategory();
        var asset = SeedAsset();
        var future = SeedLine(asset, new DateOnly(2026, 11, 30), 100m);

        var result = await Handler().HandleAsync(
            new PostDueDepreciationsCommand(_companyId, AsOf), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.BookedCount);
        Assert.Equal(0, result.Value!.SkippedCount);
        Assert.Null(result.Value!.VoucherNo);
        Assert.Empty(_assets.AddedGlEntries);
        Assert.Equal(AssetScheduleStatus.Scheduled, future.Status);
        Assert.Equal(0m, asset.AccumulatedDepreciation);
        Assert.Equal(AssetStatus.Capitalized, asset.Status);
    }

    /// <summary>
    /// A Scheduled line on a non-Capitalized holder is corrupt data (lines exist only after
    /// capitalization): skipped with a reason, never failing the healthy rows.
    /// </summary>
    [Fact]
    public async Task Run_NonCapitalizedHolder_SkippedWithReason()
    {
        SeedCategory();
        var draft = SeedAsset(status: AssetStatus.Draft);
        var line = SeedLine(draft, new DateOnly(2026, 10, 31), 100m);

        var result = await Handler().HandleAsync(
            new PostDueDepreciationsCommand(_companyId, AsOf), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.BookedCount);
        var skip = Assert.Single(result.Value!.Skipped);
        Assert.Equal(line.Id, skip.ScheduleLineId);
        Assert.Equal(draft.Id, skip.AssetId);
        Assert.Equal("unexpected_asset_status_draft", skip.Reason);
        Assert.Null(result.Value!.VoucherNo);
        Assert.Empty(_assets.AddedGlEntries);
        Assert.Equal(0m, draft.AccumulatedDepreciation);
    }

    /// <summary>Empty due set succeeds with zeros and no voucher.</summary>
    [Fact]
    public async Task Run_EmptyDueSet_SucceedsWithZeros()
    {
        SeedCategory();
        SeedAsset();

        var result = await Handler().HandleAsync(
            new PostDueDepreciationsCommand(_companyId, AsOf), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.BookedCount);
        Assert.Equal(0m, result.Value!.TotalBooked);
        Assert.Equal(0, result.Value!.SkippedCount);
        Assert.Null(result.Value!.VoucherNo);
        Assert.Empty(_assets.AddedGlEntries);
    }

    // ----------------------------------------------------------------------- rejection paths

    /// <summary>
    /// Frozen ScheduleDate fails the WHOLE run (operator problem, spec AC-04): zero writes, the
    /// line stays Scheduled and the accumulated total is intact.
    /// </summary>
    [Fact]
    public async Task Run_FrozenLineDate_FailsWholeRunWithZeroWrites()
    {
        _companies.Company!.FrozenAccountsDate = new DateOnly(2026, 10, 31);
        SeedCategory();
        var asset = SeedAsset();
        var line = SeedLine(asset, new DateOnly(2026, 10, 31), 100m);

        var result = await Handler().HandleAsync(
            new PostDueDepreciationsCommand(_companyId, AsOf), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, result.Error!.Code);
        Assert.Empty(_assets.AddedGlEntries);
        Assert.Equal(AssetScheduleStatus.Scheduled, line.Status);
        Assert.Equal(0m, asset.AccumulatedDepreciation);
        Assert.Equal(AssetStatus.Capitalized, asset.Status);
    }

    /// <summary>
    /// Spec AS-06 groundwork: a RowVersion race on ANY touched asset aborts the whole run with
    /// zero writes (no Booked marks, no accumulated moves, no GL).
    /// </summary>
    [Fact]
    public async Task Run_StaleRowVersion_AbortsWholeRunWithZeroWrites()
    {
        SeedCategory();
        var asset = SeedAsset();
        var line = SeedLine(asset, new DateOnly(2026, 10, 31), 100m);
        _assets.FailNextAssetUpdate = true;

        var result = await Handler().HandleAsync(
            new PostDueDepreciationsCommand(_companyId, AsOf), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
        Assert.Empty(_assets.AddedGlEntries);
        Assert.Equal(AssetScheduleStatus.Scheduled, line.Status);
        Assert.Equal(0m, asset.AccumulatedDepreciation);
        Assert.Equal(AssetStatus.Capitalized, asset.Status);
    }

    /// <summary>
    /// A breached salvage floor (schedule drifted past gross − salvage) aborts the run loudly.
    /// </summary>
    [Fact]
    public async Task Run_DepreciatedPastSalvage_FailsWithTypedCodeAndZeroWrites()
    {
        SeedCategory();
        var asset = SeedAsset(gross: 1000m, salvage: 100m, accumulated: 850m);
        var line = SeedLine(asset, new DateOnly(2026, 10, 31), 100m); // 950 > 900 base

        var result = await Handler().HandleAsync(
            new PostDueDepreciationsCommand(_companyId, AsOf), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssetErrorCodes.DepreciatedPastSalvage, result.Error!.Code);
        Assert.Empty(_assets.AddedGlEntries);
        Assert.Equal(AssetScheduleStatus.Scheduled, line.Status);
        Assert.Equal(850m, asset.AccumulatedDepreciation);
    }
}
