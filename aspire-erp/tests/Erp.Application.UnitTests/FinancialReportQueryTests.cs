using Erp.Application.DTOs;
using Erp.Application.Features.GeneralLedger.Queries;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// tasks.md 2.5 - the four Financial Reporting queries exercised through their CQRS handlers
/// against in-memory repository doubles (Constitution I.2/I.3): the pinned general-ledger contract
/// (page ordering, AND-combined filters, take limits and FULL-set totals), plan.md §4's trial
/// balance with its zero-discrepancy acceptance, the balance sheet's natural signs plus its
/// 0.0001 tolerance, and the P&amp;L's revenue − COGS − expenses formula.
/// </summary>
/// <remarks>
/// The seeds are always a ZERO-SUM ledger (Constitution III.1), because that is what the posting
/// engine can actually write - a report test that invented an impossible ledger would prove
/// numbers no real system could ever produce. The one deliberately unbalanced quantity is the
/// presentation sign per section, which is exactly what these tests pin down.
/// </remarks>
public sealed class FinancialReportQueryTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly FakeGLEntryRepository _ledger = new();

    private readonly Account _cash;     // 1110 - Asset (debit-natured)
    private readonly Account _payable;  // 2110 - Liability (credit-natured)
    private readonly Account _capital;  // 3000 - Equity (credit-natured)
    private readonly Account _sales;    // 4110 - Income (credit-natured)
    private readonly Account _supplies; // 5110 - Expense, operating
    private readonly Account _cogs;     // 5210 - Expense, typed COGS

    public FinancialReportQueryTests()
    {
        _cash = LeafAccount("1110", "Cash and Cash Equivalents", AccountRootType.Asset);
        _payable = LeafAccount("2110", "Accounts Payable", AccountRootType.Liability);
        _capital = LeafAccount("3000", "Share Capital", AccountRootType.Equity);
        _sales = LeafAccount("4110", "Sales Revenue", AccountRootType.Income);
        _supplies = LeafAccount("5110", "Office Supplies", AccountRootType.Expense, AccountType.Expense);
        _cogs = LeafAccount("5210", "Cost of Goods Sold", AccountRootType.Expense, AccountType.COGS);
    }

    // ---------------------------------------------------------------------------------------------
    // GET general-ledger - pinned Task 2.6 contract
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The page is chronological (PostingDate ASC, Id ASC) while the totals describe the FULL
    /// filtered set: with take = 2 the response shows two rows but the totals still add up every
    /// seeded row - the exact behaviour the audit viewer's footer badge depends on.
    /// </summary>
    [Fact]
    public async Task GeneralLedger_PageIsChronological_AndTotalsCoverTheFullFilteredSet()
    {
        _ledger.Seed(
            LedgerRow(_cash, 500m, 0m, new DateOnly(2026, 1, 10), id: 3),
            LedgerRow(_supplies, 0m, 200m, new DateOnly(2026, 1, 5), id: 1),
            LedgerRow(_cash, 0m, 100m, new DateOnly(2026, 1, 10), id: 2));

        var report = await GeneralLedgerAsync(take: 2);

        Assert.Equal(2, report.Items.Count);
        Assert.Equal(new long[] { 1L, 2L }, report.Items.Select(item => item.Id).ToArray());
        Assert.Equal(new DateOnly(2026, 1, 5), report.Items[0].PostingDate);

        // Account columns ride along with every ledger row (the pinned entry shape).
        Assert.Equal("1110", report.Items[1].AccountCode);
        Assert.Equal("Cash and Cash Equivalents", report.Items[1].AccountName);
        Assert.Equal(AccountRootType.Asset, report.Items[1].RootType);
        Assert.Equal("USD", report.Items[1].AccountCurrency);

        // Totals over the FULL filter - the two visible rows alone would sum to 0 debit.
        Assert.Equal(500m, report.TotalDebit);
        Assert.Equal(300m, report.TotalCredit);
        Assert.Equal(200m, report.Difference);
        Assert.NotEqual(report.Items.Sum(item => item.Debit), report.TotalDebit);
    }

    /// <summary>
    /// Account, voucher, voucher type and the date range narrow the report with AND: every
    /// populated filter must match for a row to survive, and a filter combination nobody matches
    /// answers with an empty page and zero totals (not an error).
    /// </summary>
    [Fact]
    public async Task GeneralLedger_FiltersCombineWithAnd()
    {
        var firstVoucher = Guid.NewGuid();
        var secondVoucher = Guid.NewGuid();

        _ledger.Seed(
            LedgerRow(_cash, 100m, 0m, new DateOnly(2026, 1, 5), id: 1, "JournalEntry", firstVoucher),
            LedgerRow(_cash, 0m, 50m, new DateOnly(2026, 2, 5), id: 2, "StockEntry", secondVoucher),
            LedgerRow(_supplies, 70m, 0m, new DateOnly(2026, 1, 5), id: 3, "JournalEntry", firstVoucher));

        var narrowed = await GeneralLedgerAsync(
            accountId: _cash.Id,
            voucherId: firstVoucher,
            voucherType: "JournalEntry",
            from: new DateOnly(2026, 1, 1),
            to: new DateOnly(2026, 1, 31));

        var only = Assert.Single(narrowed.Items);
        Assert.Equal(1L, only.Id);
        Assert.Equal(100m, narrowed.TotalDebit);
        Assert.Equal(0m, narrowed.TotalCredit);

        var noMatch = await GeneralLedgerAsync(accountId: _cash.Id, voucherType: "PurchaseInvoice");
        Assert.Empty(noMatch.Items);
        Assert.Equal(0m, noMatch.TotalDebit);
        Assert.Equal(0m, noMatch.TotalCredit);
        Assert.Equal(0m, noMatch.Difference);
    }

    /// <summary>
    /// take is a LIMIT, not a contract: unset (0/negative) falls back to the documented default of
    /// 500, an outrageous 9000 is capped at 5000 - and in both cases the totals still describe
    /// every seeded row rather than the truncated page.
    /// </summary>
    [Fact]
    public async Task GeneralLedger_TakeFallsBackToDefaultAndIsCappedAtMaximum()
    {
        for (var id = 1; id <= 5100; id++)
        {
            _ledger.Seed(LedgerRow(_cash, 1m, 0m, new DateOnly(2026, 1, 1), id));
        }

        var defaulted = await GeneralLedgerAsync(take: 0);
        Assert.Equal(500, defaulted.Items.Count);
        Assert.Equal(500L, defaulted.Items[^1].Id); // the FIRST 500 rows, in order
        Assert.Equal(5100m, defaulted.TotalDebit);

        var capped = await GeneralLedgerAsync(take: 9000);
        Assert.Equal(5000, capped.Items.Count);
        Assert.Equal(5100m, capped.TotalDebit);
    }

    /// <summary>An empty ledger is a valid report: no rows, zeros everywhere - never a failure.</summary>
    [Fact]
    public async Task GeneralLedger_EmptyLedger_ReturnsEmptyItemsAndZeroTotals()
    {
        var report = await GeneralLedgerAsync();

        Assert.Empty(report.Items);
        Assert.Equal(0m, report.TotalDebit);
        Assert.Equal(0m, report.TotalCredit);
        Assert.Equal(0m, report.Difference);
    }

    // ---------------------------------------------------------------------------------------------
    // GET trial-balance - plan.md §4 + tasks.md 2.5 acceptance ("zero discrepancy")
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// THE acceptance of tasks.md 2.5: a zero-sum ledger (the only kind the posting engine writes)
    /// produces totalDebit == totalCredit and therefore a difference of exactly 0.0000, with one
    /// row per account that moved, ordered by AccountCode and carrying the technical
    /// Debit − Credit net of each account.
    /// </summary>
    [Fact]
    public async Task TrialBalance_BalancedLedger_ReportsZeroDiscrepancy()
    {
        _ledger.Seed(
            LedgerRow(_cash, 500m, 0m, new DateOnly(2026, 3, 1), id: 1),
            LedgerRow(_supplies, 200m, 0m, new DateOnly(2026, 3, 2), id: 2),
            LedgerRow(_payable, 0m, 700m, new DateOnly(2026, 3, 2), id: 3));

        var cutoff = new DateOnly(2026, 3, 31);
        var report = await TrialBalanceAsync(cutoff);

        Assert.Equal(cutoff, report.AsOfDate);
        Assert.Equal(700m, report.TotalDebit);
        Assert.Equal(700m, report.TotalCredit);
        Assert.Equal(0m, report.Difference); // "Trial balance reports zero discrepancy"

        Assert.Equal(new[] { "1110", "2110", "5110" }, report.Rows.Select(row => row.AccountCode).ToArray());
        Assert.Equal(500m, report.Rows[0].NetBalance);     // Asset:  Debit − Credit
        Assert.Equal(-700m, report.Rows[1].NetBalance);    // Liability shown NEGATIVE - a trial
        Assert.Equal(200m, report.Rows[2].NetBalance);     // balance is a Dr/Cr worksheet.
        Assert.Equal(AccountRootType.Liability, report.Rows[1].RootType);
    }

    /// <summary>
    /// plan.md §4's cutoff is inclusive and only accounts WITH movement appear: a row posted after
    /// <c>asOfDate</c> is invisible to that statement, and untouched accounts are absent instead of
    /// rendering zero rows.
    /// </summary>
    [Fact]
    public async Task TrialBalance_ExcludesRowsAfterTheCutoff_AndAccountsWithoutMovement()
    {
        // March and April each close their own voucher (500 Dr / 500 Cr, then 999 Dr / 999 Cr),
        // so every cutoff sees a zero-sum slice - the only ledger the posting engine writes.
        _ledger.Seed(
            LedgerRow(_cash, 500m, 0m, new DateOnly(2026, 3, 1), id: 1),
            LedgerRow(_payable, 0m, 500m, new DateOnly(2026, 3, 1), id: 2),
            LedgerRow(_cash, 999m, 0m, new DateOnly(2026, 4, 10), id: 3),
            LedgerRow(_payable, 0m, 999m, new DateOnly(2026, 4, 10), id: 4));

        var march = await TrialBalanceAsync(new DateOnly(2026, 3, 31));
        Assert.Equal(500m, march.TotalDebit);
        Assert.Equal(500m, march.TotalCredit);
        Assert.Equal(0m, march.Difference);

        // Only the two accounts that moved before the cutoff appear, ordered by AccountCode.
        Assert.Equal(new[] { "1110", "2110" }, march.Rows.Select(row => row.AccountCode).ToArray());
        Assert.DoesNotContain(march.Rows, row => row.AccountCode == "3000"); // no movement, no row

        var yearEnd = await TrialBalanceAsync(new DateOnly(2026, 12, 31));
        Assert.Equal(1499m, yearEnd.TotalDebit);  // the April rows join the statement
        Assert.Equal(1499m, yearEnd.TotalCredit);
        Assert.Equal(0m, yearEnd.Difference);
    }

    // ---------------------------------------------------------------------------------------------
    // GET balance-sheet - Assets = Liabilities + Equity, natural signs, 0.0001 tolerance
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Sections are built from the NATURAL sign of their root (Asset = Debit − Credit, Liability /
    /// Equity = Credit − Debit) and Income / Expense never reach the balance sheet - they are the
    /// profit &amp; loss. Because this seed leaves the period unclosed, the equation is off by
    /// exactly the net profit, so <c>Balanced</c> correctly reports false.
    /// </summary>
    [Fact]
    public async Task BalanceSheet_PresentsNaturalSignsPerRoot_AndExcludesIncomeAndExpense()
    {
        _ledger.Seed(
            LedgerRow(_cash, 1200m, 0m, new DateOnly(2026, 6, 30), id: 1),     // Asset    +1200
            LedgerRow(_payable, 0m, 600m, new DateOnly(2026, 6, 30), id: 2),    // Liability 600 (credit)
            LedgerRow(_capital, 0m, 400m, new DateOnly(2026, 6, 30), id: 3),    // Equity    400 (credit)
            LedgerRow(_sales, 0m, 700m, new DateOnly(2026, 6, 30), id: 4),      // Income  - excluded
            LedgerRow(_supplies, 500m, 0m, new DateOnly(2026, 6, 30), id: 5));  // Expense - excluded

        var report = await BalanceSheetAsync(new DateOnly(2026, 6, 30));

        var assetRow = Assert.Single(report.Assets.Rows);
        Assert.Equal("1110", assetRow.AccountCode);
        Assert.Equal(1200m, assetRow.Balance);
        Assert.Equal(1200m, report.Assets.Total);

        var liabilityRow = Assert.Single(report.Liabilities.Rows);
        Assert.Equal(600m, liabilityRow.Balance); // credit-natured: presented POSITIVE
        Assert.Equal(600m, report.Liabilities.Total);

        var equityRow = Assert.Single(report.Equity.Rows);
        Assert.Equal("3000", equityRow.AccountCode);
        Assert.Equal(400m, equityRow.Balance);
        Assert.Equal(400m, report.Equity.Total);

        Assert.DoesNotContain(report.Assets.Rows, row => row.AccountCode == "4110");
        Assert.DoesNotContain(report.Liabilities.Rows, row => row.AccountCode == "5110");
        Assert.DoesNotContain(report.Equity.Rows, row => row.AccountCode == "5110");

        // Unclosed period: the residual of the equation IS the net profit (asserted against the
        // P&L below), so "assets = liabilities + equity" is false by exactly that amount.
        Assert.Equal(200m, report.Assets.Total - (report.Liabilities.Total + report.Equity.Total));
        Assert.False(report.Balanced);
    }

    /// <summary>
    /// With no Income/Expense activity the three sections close the equation exactly, which is the
    /// <c>Balanced == true</c> branch of the pinned contract.
    /// </summary>
    [Fact]
    public async Task BalanceSheet_ReportsBalancedWhenTheSectionsCloseTheEquation()
    {
        _ledger.Seed(
            LedgerRow(_cash, 1000m, 0m, new DateOnly(2026, 6, 30), id: 1),
            LedgerRow(_payable, 0m, 600m, new DateOnly(2026, 6, 30), id: 2),
            LedgerRow(_capital, 0m, 400m, new DateOnly(2026, 6, 30), id: 3));

        var report = await BalanceSheetAsync(new DateOnly(2026, 6, 30));

        Assert.Equal(1000m, report.Assets.Total);
        Assert.Equal(600m, report.Liabilities.Total);
        Assert.Equal(400m, report.Equity.Total);
        Assert.True(report.Balanced);
    }

    /// <summary>The tolerance is INCLUSIVE: a gap of exactly 0.0001 still counts as balanced.</summary>
    [Fact]
    public async Task BalanceSheet_GapOfExactlyTheToleranceIsStillBalanced()
    {
        _ledger.Seed(
            LedgerRow(_cash, 100.0001m, 0m, new DateOnly(2026, 6, 30), id: 1),
            LedgerRow(_capital, 0m, 100m, new DateOnly(2026, 6, 30), id: 2));

        var report = await BalanceSheetAsync(new DateOnly(2026, 6, 30));

        Assert.Equal(0.0001m, report.Assets.Total - report.Equity.Total);
        Assert.True(report.Balanced);
    }

    /// <summary>One ten-thousandth above the tolerance and the statement reports the gap.</summary>
    [Fact]
    public async Task BalanceSheet_GapAboveTheToleranceReportsUnbalanced()
    {
        _ledger.Seed(
            LedgerRow(_cash, 100.0002m, 0m, new DateOnly(2026, 6, 30), id: 1),
            LedgerRow(_capital, 0m, 100m, new DateOnly(2026, 6, 30), id: 2));

        var report = await BalanceSheetAsync(new DateOnly(2026, 6, 30));

        Assert.Equal(0.0002m, report.Assets.Total - report.Equity.Total);
        Assert.False(report.Balanced);
    }

    // ---------------------------------------------------------------------------------------------
    // GET profit-and-loss - Revenue - COGS - Expenses = Net Profit (tasks.md 2.5)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The Expense root splits by <c>Account.Type</c> (COGS vs operating), revenue is presented
    /// CREDIT-positive so an ordinary subtraction yields the result of the period, and an Asset
    /// account can never leak into a P&amp;L section.
    /// </summary>
    [Fact]
    public async Task ProfitAndLoss_SplitsCogsFromOperatingExpensesAndComputesNetProfit()
    {
        _ledger.Seed(
            LedgerRow(_sales, 100m, 2100m, new DateOnly(2026, 6, 30), id: 1), // revenue 2000
            LedgerRow(_cogs, 400m, 0m, new DateOnly(2026, 6, 30), id: 2),     // COGS     400
            LedgerRow(_supplies, 350m, 0m, new DateOnly(2026, 6, 30), id: 3), // opex     350
            LedgerRow(_supplies, 250m, 0m, new DateOnly(2026, 6, 30), id: 4), // opex     250
            LedgerRow(_cash, 1000m, 0m, new DateOnly(2026, 6, 30), id: 5));   // Asset - excluded

        var report = await ProfitAndLossAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        var revenueRow = Assert.Single(report.Revenue.Rows);
        Assert.Equal("4110", revenueRow.AccountCode);
        Assert.Equal(2000m, revenueRow.Balance); // Credit − Debit, not the raw 100/2100
        Assert.Equal(2000m, report.Revenue.Total);

        var cogsRow = Assert.Single(report.Cogs.Rows);
        Assert.Equal("5210", cogsRow.AccountCode);
        Assert.Equal(400m, report.Cogs.Total);

        var expenseRow = Assert.Single(report.Expenses.Rows);
        Assert.Equal("5110", expenseRow.AccountCode);
        Assert.Equal(600m, report.Expenses.Total); // the two operating lines summed

        Assert.Equal(1000m, report.NetProfit); // 2000 - 400 - 600

        Assert.DoesNotContain(report.Revenue.Rows, row => row.AccountCode == "1110");
        Assert.DoesNotContain(report.Cogs.Rows, row => row.AccountCode == "1110");
        Assert.DoesNotContain(report.Expenses.Rows, row => row.AccountCode == "1110");
    }

    /// <summary>The period bounds are inclusive on both ends - a sale outside them changes nothing.</summary>
    [Fact]
    public async Task ProfitAndLoss_RespectsThePeriodBounds()
    {
        _ledger.Seed(
            LedgerRow(_sales, 0m, 1000m, new DateOnly(2026, 1, 15), id: 1),
            LedgerRow(_sales, 0m, 500m, new DateOnly(2026, 3, 15), id: 2),
            LedgerRow(_supplies, 200m, 0m, new DateOnly(2026, 2, 15), id: 3));

        var report = await ProfitAndLossAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 28));

        Assert.Equal(1000m, report.Revenue.Total);  // the March sale is outside the period
        Assert.Equal(0m, report.Cogs.Total);
        Assert.Equal(200m, report.Expenses.Total);
        Assert.Equal(800m, report.NetProfit);
    }

    // ---------------------------------------------------------------------------------------------
    // Cross-report identity: the balance-sheet residual IS the P&L's net profit
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The two statements must tell ONE story: for the same period,
    /// <c>assets − (liabilities + equity)</c> equals <c>revenue − cogs − expenses</c>, which is
    /// precisely why the balance sheet flags an unclosed period as unbalanced. A mismatch would
    /// mean the reports disagree about what the ledger says.
    /// </summary>
    [Fact]
    public async Task BalanceSheetResidual_EqualsProfitAndLossNetProfit()
    {
        // Cash 900 Dr / 600 Cr / 400 Cr / 700 Cr / 500 Dr / 300 Dr => Sigma Dr = Sigma Cr = 1700.
        // The identity below is a consequence of that zero-sum law, so an unbalanced seed would
        // prove nothing about the reports - only about my arithmetic.
        _ledger.Seed(
            LedgerRow(_cash, 900m, 0m, new DateOnly(2026, 6, 30), id: 1),
            LedgerRow(_payable, 0m, 600m, new DateOnly(2026, 6, 30), id: 2),
            LedgerRow(_capital, 0m, 400m, new DateOnly(2026, 6, 30), id: 3),
            LedgerRow(_sales, 0m, 700m, new DateOnly(2026, 6, 30), id: 4),
            LedgerRow(_supplies, 500m, 0m, new DateOnly(2026, 6, 30), id: 5),
            LedgerRow(_cogs, 300m, 0m, new DateOnly(2026, 6, 30), id: 6));

        var cutoff = new DateOnly(2026, 6, 30);
        var balanceSheet = await BalanceSheetAsync(cutoff);
        var profitAndLoss = await ProfitAndLossAsync(new DateOnly(2026, 1, 1), cutoff);

        var residual = balanceSheet.Assets.Total
            - (balanceSheet.Liabilities.Total + balanceSheet.Equity.Total);

        Assert.Equal(profitAndLoss.NetProfit, residual);
        Assert.Equal(700m - 300m - 500m, profitAndLoss.NetProfit); // revenue - COGS - opex = -100
        Assert.Equal(-100m, residual); // 900 - (600 + 400): the deficit the P&L just reported
        Assert.False(balanceSheet.Balanced); // unclosed period, exactly as documented
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------

    private Task<GeneralLedgerReportDto> GeneralLedgerAsync(
        Guid? accountId = null,
        Guid? voucherId = null,
        string? voucherType = null,
        DateOnly? from = null,
        DateOnly? to = null,
        int take = 0)
        => new GetGeneralLedgerQueryHandler(_ledger).HandleAsync(
            new GetGeneralLedgerQuery(_companyId, accountId, voucherId, voucherType, from, to, take));

    private Task<TrialBalanceReportDto> TrialBalanceAsync(DateOnly asOfDate)
        => new GetTrialBalanceQueryHandler(_ledger).HandleAsync(
            new GetTrialBalanceQuery(_companyId, asOfDate));

    private Task<BalanceSheetReportDto> BalanceSheetAsync(DateOnly asOfDate)
        => new GetBalanceSheetQueryHandler(_ledger).HandleAsync(
            new GetBalanceSheetQuery(_companyId, asOfDate));

    private Task<ProfitAndLossReportDto> ProfitAndLossAsync(DateOnly from, DateOnly to)
        => new GetProfitAndLossQueryHandler(_ledger).HandleAsync(
            new GetProfitAndLossQuery(_companyId, from, to));

    /// <summary>A posting (leaf) account of the report company - the doubles never validate the COA.</summary>
    private Account LeafAccount(
        string code,
        string name,
        AccountRootType rootType,
        AccountType type = AccountType.Other)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            AccountCode = code,
            AccountName = name,
            RootType = rootType,
            Type = type,
            IsGroup = false,
            IsActive = true,
        };

    /// <summary>
    /// One ledger row, already carrying its account (the repository joins it - see
    /// <see cref="Fakes.FakeGLEntryRepository"/>).
    /// </summary>
    private GLEntry LedgerRow(
        Account account,
        decimal debit,
        decimal credit,
        DateOnly postingDate,
        long id = 0,
        string voucherType = "JournalEntry",
        Guid? voucherId = null)
        => new()
        {
            Id = id,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            PostingDate = postingDate,
            AccountId = account.Id,
            Account = account,
            Debit = debit,
            Credit = credit,
            AccountCurrency = "USD",
            VoucherType = voucherType,
            VoucherNo = "JV-2026-00001",
            VoucherId = voucherId ?? Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
}

