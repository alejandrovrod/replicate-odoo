using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Services;

/// <summary>
/// Constitution Article III.1 (Partida Doble / Zero-Sum Invariant): the posting pipeline MUST
/// verify that a voucher's debits minus credits round to exactly 0.0000 before anything is
/// committed, throwing <see cref="DoubleEntryImbalanceException"/> otherwise.
/// </summary>
/// <remarks>
/// Pure C# (Constitution I.2) so the guard is unit-tested directly from
/// <c>tests/Erp.Application.UnitTests</c> without a database; <c>StockPostingService</c> calls it
/// after building the GL lines and BEFORE SaveChanges, so a failure leaves zero rows behind.
/// </remarks>
public static class DoubleEntryGuard
{
    /// <summary>Scaling factor of monetary amounts in GLEntry (decimal(18,4)).</summary>
    private const int Scale = 4;

    /// <summary>Sums the lines and throws when the balance does not round to 0.0000.</summary>
    /// <exception cref="DoubleEntryImbalanceException">debits - credits != 0.0000.</exception>
    public static void EnsureBalanced(IReadOnlyCollection<GLEntry> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var totalDebit = 0m;
        var totalCredit = 0m;

        foreach (var line in lines)
        {
            totalDebit += line.Debit;
            totalCredit += line.Credit;
        }

        totalDebit = Math.Round(totalDebit, Scale, MidpointRounding.AwayFromZero);
        totalCredit = Math.Round(totalCredit, Scale, MidpointRounding.AwayFromZero);

        if (totalDebit - totalCredit != 0m)
        {
            throw new DoubleEntryImbalanceException(totalDebit, totalCredit);
        }
    }
}
