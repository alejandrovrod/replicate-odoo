using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure C# rules for staging bank lines (invariant BN-02). No EF Core, no NuGet packages -
/// Constitution Article I.2 keeps Erp.Domain dependency-free, so every rule here is unit-tested
/// without a database (mirrors <see cref="JournalEntryValidator"/>).
/// </summary>
public static class BankTransactionValidator
{
    /// <summary>
    /// Deposit / withdrawal mutual exclusivity (spec BN-02):
    /// <c>Deposit &gt;= 0, Withdrawal &gt;= 0, Deposit x Withdrawal == 0</c>.
    /// </summary>
    /// <exception cref="BankingValidationException">
    /// A side is negative (<c>negative_transaction_amount</c>), or both sides are positive
    /// (<c>both_sides_posted</c>).
    /// </exception>
    public static void EnsureValidSides(decimal deposit, decimal withdrawal)
    {
        if (deposit < 0m || withdrawal < 0m)
        {
            throw new BankingValidationException(
                BankingErrorCodes.NegativeTransactionAmount,
                $"Bank transaction sides must not be negative (received deposit {deposit:0.####}, "
                + $"withdrawal {withdrawal:0.####}).");
        }

        if (deposit > 0m && withdrawal > 0m)
        {
            throw new BankingValidationException(
                BankingErrorCodes.BothSidesPosted,
                $"Bank transaction must be either a deposit or a withdrawal, not both "
                + $"(received deposit {deposit:0.####}, withdrawal {withdrawal:0.####}).");
        }
    }
}
