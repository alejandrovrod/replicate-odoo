using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Services;

/// <summary>
/// One P&amp;L leaf balance read from the ledger (credit-normal: <c>Balance = ΣCredit − ΣDebit</c>).
/// Zero balances never reach the calculator — the repository filters them (fixpoint, spec FC-05).
/// </summary>
/// <param name="AccountId">P&amp;L leaf being closed.</param>
/// <param name="RootType">Must be Income or Expense, else the tripwire fires (spec FC-02).</param>
/// <param name="Balance">Credit-normal balance, non-zero.</param>
public sealed record ClosingBalanceInput(Guid AccountId, AccountRootType RootType, decimal Balance);

/// <summary>One derived offset line: the zeroing posting for a single leaf.</summary>
public sealed record ClosingOffsetLine(Guid AccountId, decimal Debit, decimal Credit);

/// <summary>
/// Result of <see cref="PeriodClosingCalculator.Compute"/>: per-leaf zeroing lines, the optional
/// retained-earnings mirror (absent on break-even, spec FC-03 scenario), and the net P&amp;L.
/// </summary>
public sealed record ClosingComputation(
    IReadOnlyList<ClosingOffsetLine> OffsetLines,
    ClosingOffsetLine? RetainedLine,
    decimal Net);

/// <summary>
/// Pure closing-math domain service (tasks.md 1.4, spec FC-01/FC-02/FC-03): credit-normal zeroing
/// with the double-entry assertion. No I/O, no EF — unit-testable in isolation.
/// </summary>
public static class PeriodClosingCalculator
{
    /// <summary>Constitution III.1 tolerance: |ΣDebit − ΣCredit| ≤ 0.0001.</summary>
    public const decimal BalanceTolerance = 0.0001m;

    /// <summary>
    /// Computes the closing set: for each P&amp;L leaf with credit-normal balance
    /// <c>b ≠ 0</c>, post <c>Debit = max(b, 0)</c> / <c>Credit = max(−b, 0)</c> (zeroing it), and
    /// post the mirror on retained earnings (<c>Debit = max(−Net, 0)</c> /
    /// <c>Credit = max(Net, 0)</c>). On break-even (<c>Net = 0</c>) no retained row is produced —
    /// a zero-value GL row is never posted (spec FC-03 scenario).
    /// </summary>
    /// <exception cref="NoClosingBalancesException">Empty input (spec FC-12).</exception>
    /// <exception cref="ClosingNonPLAccountException">A non-P&amp;L balance sneaks in (spec FC-02).</exception>
    /// <exception cref="DoubleEntryImbalanceException">Computed set violates ΣD == ΣC (spec FC-01).</exception>
    public static ClosingComputation Compute(
        IReadOnlyList<ClosingBalanceInput> balances,
        Guid retainedEarningsAccountId)
    {
        if (balances.Count == 0)
        {
            throw new NoClosingBalancesException("(preview)");
        }

        foreach (var input in balances)
        {
            if (input.RootType != AccountRootType.Income && input.RootType != AccountRootType.Expense)
            {
                throw new ClosingNonPLAccountException(input.AccountId, input.RootType.ToString());
            }
        }

        var nonZero = balances.Where(b => b.Balance != 0).ToList();
        if (nonZero.Count == 0)
        {
            throw new NoClosingBalancesException("(preview)");
        }

        var offsets = nonZero
            .Select(b => new ClosingOffsetLine(
                b.AccountId,
                Debit: b.Balance > 0 ? b.Balance : 0m,
                Credit: b.Balance < 0 ? -b.Balance : 0m))
            .ToList();

        // Net = Σ IncomeBalances − Σ ExpenseBalances where each balance is already credit-normal,
        // so Net is simply Σ b (spec §1 Net P&L definition).
        var net = nonZero.Sum(b => b.Balance);

        ClosingOffsetLine? retained = net != 0
            ? new ClosingOffsetLine(
                retainedEarningsAccountId,
                Debit: net < 0 ? -net : 0m,
                Credit: net > 0 ? net : 0m)
            : null;

        var totalDebit = offsets.Sum(l => l.Debit) + (retained?.Debit ?? 0m);
        var totalCredit = offsets.Sum(l => l.Credit) + (retained?.Credit ?? 0m);

        if (Math.Abs(totalDebit - totalCredit) > BalanceTolerance)
        {
            throw new DoubleEntryImbalanceException(totalDebit, totalCredit);
        }

        return new ClosingComputation(offsets, retained, net);
    }
}
