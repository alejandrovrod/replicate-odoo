using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Services;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Constitution Article III.1 (Partida Doble / Zero-Sum Invariant) as tested by Task 3.2/D12:
/// debits minus credits must round to exactly 0.0000 at four decimals, or the posting throws
/// before a single row reaches the database.
/// </summary>
public sealed class DoubleEntryGuardTests
{
    private static GLEntry Line(decimal debit, decimal credit) =>
        new() { Debit = debit, Credit = credit };

    [Fact]
    public void EnsureBalanced_BalancedVoucher_Passes()
    {
        var lines = new List<GLEntry>
        {
            Line(debit: 620.00m, credit: 0m),
            Line(debit: 0m, credit: 620.00m),
        };

        DoubleEntryGuard.EnsureBalanced(lines); // must not throw
    }

    [Fact]
    public void EnsureBalanced_MultiLineVoucher_Passes()
    {
        var lines = new List<GLEntry>
        {
            Line(debit: 100.0001m, credit: 0m),
            Line(debit: 250.5m, credit: 0m),
            Line(debit: 0m, credit: 350.5001m),
        };

        DoubleEntryGuard.EnsureBalanced(lines); // must not throw
    }

    [Fact]
    public void EnsureBalanced_ImbalancedVoucher_ThrowsWithBothTotals()
    {
        var lines = new List<GLEntry>
        {
            Line(debit: 620.00m, credit: 0m),
            Line(debit: 0m, credit: 619.9999m),
        };

        var ex = Assert.Throws<DoubleEntryImbalanceException>(() => DoubleEntryGuard.EnsureBalanced(lines));

        Assert.Equal(StockErrorCodes.DoubleEntryImbalance, ex.Code);
        Assert.Equal(620.00m, ex.TotalDebit);
        Assert.Equal(619.9999m, ex.TotalCredit);
        Assert.Contains("0.0001", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureBalanced_RoundingWithinFourDecimals_IsAccepted()
    {
        // 0.00004 rounds away from zero at scale 4 -> 0.0000, so the guard accepts it.
        var lines = new List<GLEntry>
        {
            Line(debit: 10.00004m, credit: 0m),
            Line(debit: 0m, credit: 10m),
        };

        DoubleEntryGuard.EnsureBalanced(lines); // must not throw
    }

    [Fact]
    public void EnsureBalanced_EmptySet_IsVacuouslyBalanced()
    {
        DoubleEntryGuard.EnsureBalanced(new List<GLEntry>()); // must not throw
    }

    [Fact]
    public void EnsureBalanced_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DoubleEntryGuard.EnsureBalanced(null!));
    }
}

