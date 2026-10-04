using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Services;

/// <summary>
/// Credit Limit Protection (spec invariant SL-02, plan.md §2): the single gate every credit
/// document must pass before it is submitted -
/// <c>Customer.OutstandingAmount + attemptedAmount &lt;= Customer.CreditLimit</c>.
/// </summary>
/// <remarks>
/// <para>Pure Domain (no EF, no NuGet - Constitution Article I.2), so the full boundary matrix is
/// unit-tested from <c>tests/Erp.Domain.UnitTests</c>.</para>
/// <para><b>plan.md §2 literal semantics.</b> A breach is <c>totalExposure &gt; CreditLimit</c>,
/// so exposure exactly AT the limit is allowed (the spec's <c>&lt;=</c> in SL-02). Two
/// short-circuits skip the check entirely: <c>BypassCreditLimitCheck</c> (spec SL-02's
/// <c>== false</c> condition) and <c>CreditLimit &lt;= 0</c> - a zero limit means the customer has
/// no credit control at all rather than "may never be invoiced"; the plan chose the permissive
/// reading and <c>CK_Customer_CreditLimit</c> guarantees the value can never actually be negative.</para>
/// </remarks>
public sealed class CreditControlEvaluator
{
    /// <summary>
    /// Rejects an exposure that would exceed the customer's credit limit.
    /// </summary>
    /// <param name="customer">Customer whose exposure is evaluated (current outstanding debt included).</param>
    /// <param name="newInvoiceAmount">Amount this attempt adds on top of the outstanding debt.</param>
    /// <exception cref="ArgumentNullException"><paramref name="customer"/> is null.</exception>
    /// <exception cref="CreditLimitExceededException">
    /// The customer has a positive limit, does not bypass the check, and
    /// <c>OutstandingAmount + newInvoiceAmount</c> exceeds it (spec SL-02).
    /// </exception>
    public static void ValidateCreditExposure(Customer customer, decimal newInvoiceAmount)
    {
        ArgumentNullException.ThrowIfNull(customer);

        if (customer.BypassCreditLimitCheck || customer.CreditLimit <= 0)
        {
            return;
        }

        var totalExposure = customer.OutstandingAmount + newInvoiceAmount;

        if (totalExposure > customer.CreditLimit)
        {
            throw new CreditLimitExceededException(
                customer.CustomerName,
                customer.CreditLimit,
                customer.OutstandingAmount,
                newInvoiceAmount
            );
        }
    }
}
