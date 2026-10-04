using Erp.Application.Features.Stock.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 3.7 / spec ST-04: the stock-entry cancellation pipeline exercised through the CQRS
/// handler against in-memory repository doubles (Constitution I.2/I.3) - the append-only
/// compensating reversal of BOTH ledgers, the "restored to pre-transaction state without
/// deleting history" acceptance, and the guarantee that every rejected cancellation writes
/// ZERO rows.
/// </summary>
public sealed class CancelStockEntryCommandHandlerTests
{
    private const string IssueVoucherNo = "MI-2026-00042";

    private const string OpeningReceiptVoucherNo = "MR-2026-00001";

    private static readonly DateOnly PostingDate = new(2026, 3, 2);

    /// <summary>A posting date INSIDE the frozen period used by the freeze test.</summary>
    private static readonly DateOnly BackDated = new(2025, 6, 15);

    private static readonly DateOnly FrozenThrough = new(2025, 12, 31);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _cogsAccountId = Guid.NewGuid();
    private readonly Guid _stockAccountId = Guid.NewGuid();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeStockRepository _stock = new();

    public CancelStockEntryCommandHandlerTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = Guid.NewGuid(),
            Name = "Acme Industrial",
        };
    }

    private CancelStockEntryCommandHandler Handler() => new(_stock, _companies);

    /// <summary>
    /// Seeds the state cancellation tests start from (a persisted aggregate, never the posting
    /// handler): an opening receipt of +20 units under ANOTHER voucher, the issue under test
    /// (-10 @ $12, already posted with its Dr COGS / Cr Stock pair), or - when
    /// <paramref name="withRows"/> is false - just the header for the rejection paths.
    /// </summary>
    private async Task<StockEntry> SeedIssue(
        DateOnly postingDate,
        Guid? companyId = null,
        bool withRows = true,
        bool cancelled = false)
    {
        var entry = new StockEntry
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = companyId ?? _companyId,
            WarehouseId = _warehouseId,
            EntryType = StockEntryType.MaterialIssue,
            PostingDate = postingDate,
            VoucherNo = IssueVoucherNo,
            CreatedAt = DateTimeOffset.UtcNow,
            IsCancelled = cancelled,
        };

        await _stock.AddStockEntryAsync(entry);

        if (!withRows)
        {
            return entry;
        }

        _stock.SeedLedger(
            NewKardexRow(entry, OpeningReceiptVoucherNo, stockEntryId: Guid.NewGuid(), qty: 20m, amount: 240m),
            NewKardexRow(entry, IssueVoucherNo, stockEntryId: entry.Id, qty: -10m, amount: -120m));

        // The issue's canonical pair: Dr 5210 COGS 120 / Cr 1310 Stock 120 (spec ST-02).
        await _stock.AddGlEntriesAsync(new[]
        {
            NewGlRow(entry, _cogsAccountId, "5210", debit: 120m, credit: 0m),
            NewGlRow(entry, _stockAccountId, "1310", debit: 0m, credit: 120m),
        });

        return entry;
    }

    private static StockLedgerEntry NewKardexRow(
        StockEntry entry,
        string voucherNo,
        Guid stockEntryId,
        decimal qty,
        decimal amount) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = entry.TenantId,
            ItemId = Guid.NewGuid(),
            WarehouseId = entry.WarehouseId,
            StockEntryId = stockEntryId,
            VoucherType = "StockEntry",
            VoucherNo = voucherNo,
            PostingDate = entry.PostingDate,
            QtyChange = qty,
            ValuationRate = 12m,
            Amount = amount,
            CreatedAt = DateTimeOffset.UtcNow,
            IsCancelled = false,
        };

    private static GLEntry NewGlRow(StockEntry entry, Guid accountId, string accountCode, decimal debit, decimal credit) =>
        new()
        {
            CompanyId = entry.CompanyId,
            PostingDate = entry.PostingDate,
            AccountId = accountId,
            Debit = debit,
            Credit = credit,
            DebitInAccountCurrency = debit,
            CreditInAccountCurrency = credit,
            AccountCurrency = "USD",
            VoucherType = "StockEntry",
            VoucherNo = entry.VoucherNo,
            VoucherId = entry.Id,
            IsCancelled = false,
            Remarks = $"StockEntry: {accountCode}",
            CreatedAt = DateTimeOffset.UtcNow,
        };

    // ------------------------------------------------------------------ happy path (spec ST-04)

    /// <summary>
    /// ST-04 / Task 3.7 acceptance: balance and financial accounts return to the
    /// pre-transaction state by APPENDING compensating rows (negated Kardex, swapped GL), with
    /// the originals left byte-identical in place - history is never deleted.
    /// </summary>
    [Fact]
    public async Task Cancel_PostedIssue_AppendsCompensatingRowsAndRestoresBothLedgers()
    {
        var entry = await SeedIssue(PostingDate);

        // What the warehouse held BEFORE the issue was posted: +20 opening receipt only.
        var onHandBeforeIssue = 20m;

        // Snapshots of the ORIGINALS taken before the cancellation: the append-only guarantee
        // (Constitution III.2) means these values must be untouched afterwards.
        var kardexBefore = _stock.PersistedLedger
            .Select(r => (r.VoucherNo, r.QtyChange, r.Amount, r.IsCancelled))
            .ToList();
        var glBefore = _stock.AddedGlEntries
            .Select(r => (r.AccountId, r.Debit, r.Credit, r.IsCancelled, r.Remarks))
            .ToList();

        var result = await Handler().HandleAsync(
            new CancelStockEntryCommand(_companyId, entry.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.Equal(1, _stock.TransactionCount); // ONE transaction (Constitution III.1/III.4)
        Assert.True(entry.IsCancelled);

        // --- Kardex: exactly one negated row appended, issue net for the voucher now zero.
        var reversal = Assert.Single(_stock.AddedLedger);
        var originalIssue = kardexBefore.Single(r => r.VoucherNo == IssueVoucherNo);
        Assert.Equal(-originalIssue.QtyChange, reversal.QtyChange);
        Assert.Equal(-originalIssue.Amount, reversal.Amount);
        Assert.True(reversal.IsCancelled);
        Assert.Equal(IssueVoucherNo, reversal.VoucherNo);
        Assert.Equal(entry.Id, reversal.StockEntryId);
        Assert.Equal(PostingDate, reversal.PostingDate);

        // Restored to the pre-transaction state: on-hand equals the opening +20 again, and the
        // voucher's own rows (original + reversal) net exactly zero.
        Assert.Equal(onHandBeforeIssue, _stock.PersistedLedger.Sum(r => r.QtyChange));
        Assert.Equal(
            0m,
            _stock.PersistedLedger
                .Where(r => r.VoucherNo == IssueVoucherNo)
                .Sum(r => r.QtyChange));

        // The originals were NEVER mutated or removed (history kept).
        Assert.Equal(
            kardexBefore,
            _stock.PersistedLedger
                .Take(kardexBefore.Count)
                .Select(r => (r.VoucherNo, r.QtyChange, r.Amount, r.IsCancelled)));

        // --- General Ledger: two swapped reversals, balanced per account and in total.
        Assert.Equal(4, _stock.AddedGlEntries.Count);
        var reversals = _stock.AddedGlEntries.Where(r => r.IsCancelled).ToList();
        Assert.Equal(2, reversals.Count);

        foreach (var original in _stock.AddedGlEntries.Where(r => !r.IsCancelled))
        {
            var swapped = Assert.Single(reversals, r => r.AccountId == original.AccountId);
            Assert.Equal(original.Credit, swapped.Debit);
            Assert.Equal(original.Debit, swapped.Credit);
            Assert.Equal(original.VoucherId, swapped.VoucherId);
            Assert.Equal(original.VoucherNo, swapped.VoucherNo);
            Assert.Equal(original.PostingDate, swapped.PostingDate);
            Assert.Equal("Cancelled: " + original.Remarks, swapped.Remarks);
        }

        // Financial accounts restored: every account nets to zero across original + reversal.
        Assert.All(
            _stock.AddedGlEntries.GroupBy(r => r.AccountId),
            group => Assert.Equal(0m, group.Sum(r => r.Debit - r.Credit)));

        // Trial-balance neutrality over the whole ledger view.
        Assert.Equal(
            _stock.AddedGlEntries.Sum(r => r.Debit),
            _stock.AddedGlEntries.Sum(r => r.Credit));

        // GL originals untouched as well.
        Assert.Equal(
            glBefore,
            _stock.AddedGlEntries.Take(glBefore.Count)
                .Select(r => (r.AccountId, r.Debit, r.Credit, r.IsCancelled, r.Remarks)));
    }

    // ----------------------------------------------------------------------- rejection paths

    [Fact]
    public async Task Cancel_UnknownEntry_FailsWithVoucherNotFoundAndWritesNothing()
    {
        var result = await Handler().HandleAsync(
            new CancelStockEntryCommand(_companyId, Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(StockErrorCodes.VoucherNotFound, result.Error!.Code);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Cancel_EntryOfAnotherCompany_FailsWithVoucherNotFoundWithoutMutatingIt()
    {
        // Company mismatch is reported as NOT FOUND: the id must not leak across companies.
        var otherCompanyId = Guid.NewGuid();
        var entry = await SeedIssue(PostingDate, companyId: otherCompanyId, withRows: false);

        // The caller cancels through ITS OWN company: the entry belongs to someone else's.
        var result = await Handler().HandleAsync(
            new CancelStockEntryCommand(_companyId, entry.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(StockErrorCodes.VoucherNotFound, result.Error!.Code);
        Assert.False(entry.IsCancelled);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_FailsWithInvalidStatusTransitionAndAppendsNoRows()
    {
        var entry = await SeedIssue(PostingDate, withRows: false, cancelled: true);

        var result = await Handler().HandleAsync(
            new CancelStockEntryCommand(_companyId, entry.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(StockErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Cancel_CompanyMissing_FailsWithCompanyNotFoundAndWritesNothing()
    {
        var entry = await SeedIssue(PostingDate, withRows: false);
        _companies.Company = null;

        var result = await Handler().HandleAsync(
            new CancelStockEntryCommand(_companyId, entry.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(StockErrorCodes.CompanyNotFound, result.Error!.Code);
        Assert.False(entry.IsCancelled);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Cancel_FrozenOriginalPeriod_FailsWithFiscalPeriodLockedAndWritesNothing()
    {
        // The reversal keeps the ORIGINAL PostingDate, so a frozen period blocks the
        // cancellation too ("posting, modification, or cancellation" - spec AC-04 wording).
        _companies.Company!.FrozenAccountsDate = FrozenThrough;
        var entry = await SeedIssue(BackDated, withRows: false);

        var result = await Handler().HandleAsync(
            new CancelStockEntryCommand(_companyId, entry.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, result.Error!.Code);
        Assert.False(entry.IsCancelled);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }
}
