using Erp.Domain.Entities;

namespace Erp.Domain.Exceptions;

/// <summary>
/// Constitution Article III.1 (literal): when the debits minus credits of a voucher do not round
/// to 0.0000 the posting transaction must throw this exception and roll back - zero GLEntry rows
/// may reach the database.
/// </summary>
public sealed class DoubleEntryImbalanceException : Exception
{
    public string Code { get; } = StockErrorCodes.DoubleEntryImbalance;

    public decimal TotalDebit { get; }

    public decimal TotalCredit { get; }

    public DoubleEntryImbalanceException(decimal totalDebit, decimal totalCredit)
        : base(
            "Double entry imbalance: total debit "
            + $"{totalDebit:0.0000} minus total credit {totalCredit:0.0000} "
            + $"is {(totalDebit - totalCredit):0.0000}, which must be exactly 0.0000 (Constitution III.1).")
    {
        TotalDebit = totalDebit;
        TotalCredit = totalCredit;
    }
}
