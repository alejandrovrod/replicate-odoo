namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when a Banking &amp; Reconciliation invariant is violated (DDD: invariants throw).
/// Carries the stable <see cref="Code"/> from <c>Erp.Domain.Entities.BankingErrorCodes</c> so
/// upper layers can map the failure to an RFC 7807 response without string-matching messages.
/// </summary>
/// <remarks>
/// Over-allocation rejections additionally carry the three money values so the RFC 7807
/// <c>detail</c> can tell the operator exactly which numbers breached the cap (the
/// <see cref="CreditLimitExceededException"/> precedent).
/// </remarks>
public sealed class BankingValidationException : Exception
{
    public string Code { get; }

    /// <summary>Amount the rejected allocation step attempted (over-allocation only).</summary>
    public decimal? AttemptedAmount { get; }

    /// <summary>Invoice outstanding balance the cap is computed from (over-allocation only).</summary>
    public decimal? InvoiceOutstanding { get; }

    /// <summary>Sum already allocated to the same invoice (over-allocation only).</summary>
    public decimal? AlreadyAllocated { get; }

    public BankingValidationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public BankingValidationException(
        string code,
        string message,
        decimal attemptedAmount,
        decimal invoiceOutstanding,
        decimal alreadyAllocated)
        : base(message)
    {
        Code = code;
        AttemptedAmount = attemptedAmount;
        InvoiceOutstanding = invoiceOutstanding;
        AlreadyAllocated = alreadyAllocated;
    }
}
