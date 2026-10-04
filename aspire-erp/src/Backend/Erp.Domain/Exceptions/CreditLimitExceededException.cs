using Erp.Domain.Entities;

namespace Erp.Domain.Exceptions;

/// <summary>
/// Credit Limit Protection (spec SL-02 / plan.md §2): a credit invoice or sales order that would
/// push <c>OutstandingAmount + attempted amount</c> past <c>Customer.CreditLimit</c> is rejected
/// BEFORE anything is posted - the command never modifies data. Thrown by
/// <see cref="Services.CreditControlEvaluator.ValidateCreditExposure"/>.
/// </summary>
/// <remarks>
/// Carries all three money values so the RFC 7807 <c>detail</c> can tell the operator exactly
/// which numbers breached the limit (spec SL-02 message shape:
/// "Credit limit $5,000 exceeded. Current: $4,600, Attempted: $650"). The <see cref="Code"/>
/// travels Domain -&gt; Application (<c>Result.Failure</c>) -&gt; Api, where the controller maps it
/// to HTTP 409 (a credit breach conflicts with the customer's existing exposure, same state-
/// conflict family as <c>invalid_status_transition</c> / <c>concurrency_conflict</c>).
/// </remarks>
public sealed class CreditLimitExceededException : Exception
{
    /// <summary>Stable failure code carried into the RFC 7807 <c>code</c> extension.</summary>
    public string Code { get; } = SellingErrorCodes.CreditLimitExceeded;

    /// <summary>Customer whose exposure was rejected (named in the message, plan.md §2).</summary>
    public string CustomerName { get; }

    /// <summary>The configured limit that was breached.</summary>
    public decimal CreditLimit { get; }

    /// <summary>Exposure booked before this attempt.</summary>
    public decimal OutstandingAmount { get; }

    /// <summary>The amount this attempt tried to add on top.</summary>
    public decimal AttemptedAmount { get; }

    public CreditLimitExceededException(
        string customerName,
        decimal creditLimit,
        decimal outstandingAmount,
        decimal attemptedAmount)
        : base(
            $"Credit limit ${creditLimit:0.####} exceeded. "
            + $"Current: ${outstandingAmount:0.####}, Attempted: ${attemptedAmount:0.####} "
            + $"(customer '{customerName}').")
    {
        CustomerName = customerName;
        CreditLimit = creditLimit;
        OutstandingAmount = outstandingAmount;
        AttemptedAmount = attemptedAmount;
    }
}
