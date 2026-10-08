using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Services;
using Erp.Application.Features.FiscalClosing;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// R-13 Fase 2 (tasks.md Phase 1 acceptance): FiscalYear guards, voucher transition matrix,
/// retained-account-agnostic closing math (profit / loss / break-even / empty / non-P&amp;L)
/// and the spec §5 error-code vocabulary. Pure domain — no I/O.
/// </summary>
public sealed class FiscalClosingTests
{
    private static FiscalYear OpenYear(string name = "FY-2025") =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            YearName = name,
            StartDate = new DateOnly(2025, 1, 1),
            EndDate = new DateOnly(2025, 12, 31),
            IsClosed = false,
        };

    private static PeriodClosingVoucher DraftVoucher() =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            FiscalYearId = Guid.NewGuid(),
            VoucherNo = "DRAFT-2025-X",
            PostingDate = new DateOnly(2025, 12, 31),
            DocumentStatus = DocumentStatus.Draft,
        };

    // ---- Task 1.1: FiscalYear boundary + EnsurePostingAllowed + Close ----

    [Fact]
    public void FiscalYear_StartDateAfterEndDate_Rejected()
    {
        var year = OpenYear();
        year.StartDate = new DateOnly(2025, 12, 31);
        year.EndDate = new DateOnly(2025, 1, 1);

        Assert.Throws<ArgumentException>(() => year.EnsureValidBoundary());
    }

    [Fact]
    public void FiscalYear_StartDateEqualsEndDate_Rejected()
    {
        var year = OpenYear();
        year.StartDate = year.EndDate;

        Assert.Throws<ArgumentException>(() => year.EnsureValidBoundary());
    }

    [Fact]
    public void FiscalYear_EnsurePostingAllowed_DateInsideClosedYear_Throws()
    {
        var year = OpenYear();
        year.Close();

        var ex = Assert.Throws<FiscalYearClosedException>(
            () => year.EnsurePostingAllowed(new DateOnly(2025, 6, 15)));
        Assert.Equal(FiscalClosingErrorCodes.FiscalYearClosed, ex.Code);
    }

    [Fact]
    public void FiscalYear_EnsurePostingAllowed_DateOutsideWindow_Throws()
    {
        var year = OpenYear();

        Assert.Throws<ClosingDateOutsideFiscalYearException>(
            () => year.EnsurePostingAllowed(new DateOnly(2026, 1, 5)));
    }

    [Fact]
    public void FiscalYear_EnsurePostingAllowed_OpenYearDateInside_Passes()
    {
        var year = OpenYear();

        year.EnsurePostingAllowed(new DateOnly(2025, 12, 31)); // must not throw
    }

    [Fact]
    public void FiscalYear_Close_SetsFlagAndTimestamp()
    {
        var year = OpenYear();

        year.Close();

        Assert.True(year.IsClosed);
        Assert.NotNull(year.ClosedAt);
    }

    [Fact]
    public void FiscalYear_Close_Twice_Throws()
    {
        var year = OpenYear();
        year.Close();

        Assert.Throws<FiscalYearClosedException>(() => year.Close());
    }

    [Fact]
    public void FiscalYear_Overlaps_AdjacentYearsDoNotOverlap()
    {
        var year = OpenYear();

        Assert.False(year.Overlaps(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        Assert.True(year.Overlaps(new DateOnly(2025, 6, 1), new DateOnly(2026, 5, 31)));
        Assert.True(year.Overlaps(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
    }

    // ---- Task 1.2: transition matrix ----

    [Fact]
    public void Voucher_SubmitFromDraft_Passes()
    {
        DraftVoucher().EnsureCanSubmit(); // must not throw
    }

    [Fact]
    public void Voucher_DoubleSubmit_ThrowsInvalidTransition()
    {
        var voucher = DraftVoucher();
        voucher.DocumentStatus = DocumentStatus.Submitted;

        var ex = Assert.Throws<FiscalClosingTransitionException>(() => voucher.EnsureCanSubmit());
        Assert.Equal(FiscalClosingErrorCodes.PeriodClosingInvalidTransition, ex.Code);
    }

    [Fact]
    public void Voucher_CancelFromDraft_ThrowsInvalidTransition()
    {
        var ex = Assert.Throws<FiscalClosingTransitionException>(() => DraftVoucher().EnsureCanCancel());
        Assert.Equal(FiscalClosingErrorCodes.PeriodClosingInvalidTransition, ex.Code);
    }

    [Fact]
    public void Voucher_SubmitFromCancelled_ThrowsAlreadyCancelled()
    {
        var voucher = DraftVoucher();
        voucher.DocumentStatus = DocumentStatus.Cancelled;

        var ex = Assert.Throws<FiscalClosingTransitionException>(() => voucher.EnsureCanSubmit());
        Assert.Equal(FiscalClosingErrorCodes.PeriodClosingAlreadyCancelled, ex.Code);
    }

    [Fact]
    public void Voucher_DoubleCancel_ThrowsAlreadyCancelled()
    {
        var voucher = DraftVoucher();
        voucher.DocumentStatus = DocumentStatus.Cancelled;

        var ex = Assert.Throws<FiscalClosingTransitionException>(() => voucher.EnsureCanCancel());
        Assert.Equal(FiscalClosingErrorCodes.PeriodClosingAlreadyCancelled, ex.Code);
    }

    [Fact]
    public void Voucher_CancelFromSubmitted_Passes()
    {
        var voucher = DraftVoucher();
        voucher.DocumentStatus = DocumentStatus.Submitted;

        voucher.EnsureCanCancel(); // must not throw
    }

    [Fact]
    public void Voucher_EmptyFiscalYearId_Rejected()
    {
        var voucher = DraftVoucher();
        voucher.FiscalYearId = Guid.Empty;

        Assert.Throws<ClosingDateOutsideFiscalYearException>(() => voucher.EnsureFiscalYearBound());
    }

    // ---- Task 1.4: closing math (mirrors AC-05 fixtures) ----

    [Fact]
    public void Calculator_ProfitFixture_CreditsRetained120k()
    {
        // Spec FC-01: Revenue 500k credit-normal, Expenses 380k debit-normal (balance −380k).
        var revenue = Guid.NewGuid();
        var expenses = Guid.NewGuid();
        var retained = Guid.NewGuid();

        var result = PeriodClosingCalculator.Compute(
            new[]
            {
                new ClosingBalanceInput(revenue, AccountRootType.Income, 500_000m),
                new ClosingBalanceInput(expenses, AccountRootType.Expense, -380_000m),
            },
            retained);

        Assert.Equal(120_000m, result.Net);
        Assert.Equal(2, result.OffsetLines.Count);

        var revenueLine = Assert.Single(result.OffsetLines, l => l.AccountId == revenue);
        Assert.Equal(500_000m, revenueLine.Debit);
        Assert.Equal(0m, revenueLine.Credit);

        var expenseLine = Assert.Single(result.OffsetLines, l => l.AccountId == expenses);
        Assert.Equal(0m, expenseLine.Debit);
        Assert.Equal(380_000m, expenseLine.Credit);

        Assert.NotNull(result.RetainedLine);
        Assert.Equal(retained, result.RetainedLine.AccountId);
        Assert.Equal(0m, result.RetainedLine.Debit);
        Assert.Equal(120_000m, result.RetainedLine.Credit);

        var totalD = result.OffsetLines.Sum(l => l.Debit) + result.RetainedLine.Debit;
        var totalC = result.OffsetLines.Sum(l => l.Credit) + result.RetainedLine.Credit;
        Assert.True(Math.Abs(totalD - totalC) <= 0.0001m);
    }

    [Fact]
    public void Calculator_LossFixture_DebitsRetained60k()
    {
        // Spec FC-02: Revenue 200k, Expenses 260k → Net −60k.
        var revenue = Guid.NewGuid();
        var expenses = Guid.NewGuid();

        var result = PeriodClosingCalculator.Compute(
            new[]
            {
                new ClosingBalanceInput(revenue, AccountRootType.Income, 200_000m),
                new ClosingBalanceInput(expenses, AccountRootType.Expense, -260_000m),
            },
            Guid.NewGuid());

        Assert.Equal(-60_000m, result.Net);
        Assert.NotNull(result.RetainedLine);
        Assert.Equal(60_000m, result.RetainedLine.Debit);
        Assert.Equal(0m, result.RetainedLine.Credit);
    }

    [Fact]
    public void Calculator_BreakEven_NoRetainedRow()
    {
        // Spec FC-03: Net 0 → leaves zeroed, NO zero-value retained row.
        var result = PeriodClosingCalculator.Compute(
            new[]
            {
                new ClosingBalanceInput(Guid.NewGuid(), AccountRootType.Income, 150_000m),
                new ClosingBalanceInput(Guid.NewGuid(), AccountRootType.Expense, -150_000m),
            },
            Guid.NewGuid());

        Assert.Equal(0m, result.Net);
        Assert.Null(result.RetainedLine);
        Assert.Equal(2, result.OffsetLines.Count);
    }

    [Fact]
    public void Calculator_EmptyInput_ThrowsNoBalances()
    {
        var ex = Assert.Throws<NoClosingBalancesException>(
            () => PeriodClosingCalculator.Compute(Array.Empty<ClosingBalanceInput>(), Guid.NewGuid()));
        Assert.Equal(FiscalClosingErrorCodes.NoClosingBalances, ex.Code);
    }

    [Fact]
    public void Calculator_NonPLInput_ThrowsClosingNonPLAccount()
    {
        var ex = Assert.Throws<ClosingNonPLAccountException>(
            () => PeriodClosingCalculator.Compute(
                new[] { new ClosingBalanceInput(Guid.NewGuid(), AccountRootType.Asset, 1_000m) },
                Guid.NewGuid()));
        Assert.Equal(FiscalClosingErrorCodes.ClosingNonPLAccount, ex.Code);
    }

    // ---- Task 1.3: error-code vocabulary ----

    [Theory]
    [InlineData(FiscalClosingErrorCodes.FiscalYearClosed, 409)]
    [InlineData(FiscalClosingErrorCodes.FiscalYearOverlap, 409)]
    [InlineData(FiscalClosingErrorCodes.DuplicateClosingForFiscalYear, 409)]
    [InlineData(FiscalClosingErrorCodes.PeriodClosingInvalidTransition, 409)]
    [InlineData(FiscalClosingErrorCodes.PeriodClosingAlreadyCancelled, 409)]
    [InlineData(FiscalClosingErrorCodes.ConcurrencyConflict, 409)]
    [InlineData(FiscalClosingErrorCodes.InvalidRetainedEarningsAccount, 400)]
    [InlineData(FiscalClosingErrorCodes.ClosingDateOutsideFiscalYear, 422)]
    [InlineData(FiscalClosingErrorCodes.NoClosingBalances, 422)]
    [InlineData(FiscalClosingErrorCodes.PeriodClosingNotFound, 404)]
    [InlineData(FiscalClosingErrorCodes.FiscalYearNotFound, 404)]
    [InlineData(FiscalClosingErrorCodes.ClosingNonPLAccount, 500)]
    [InlineData(FiscalClosingErrorCodes.DoubleEntryImbalance, 500)]
    public void ErrorCodes_MapToSpecSection5HttpStatus(string code, int expectedStatus)
    {
        Assert.Equal(expectedStatus, FiscalClosingHttpStatus.StatusFor(code));
    }
}
