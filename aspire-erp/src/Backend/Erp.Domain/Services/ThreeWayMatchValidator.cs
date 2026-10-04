using System.Globalization;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Services;

/// <summary>
/// Validates three-way match conditions for purchase invoicing (Task 4.4 / spec BY-03): a bill may
/// never exceed what its purchase receipt line still has available, i.e.
/// <c>attemptingToBillQty &lt;= receivedQty - previouslyBilledQty</c> on the CUMULATIVE model,
/// where <c>previouslyBilledQty</c> is the sum of every invoice line already posted against that
/// receipt line (spec BY-06 allows several clerks to bill one receipt progressively).
/// </summary>
/// <remarks>
/// The thrown messages are the VERBATIM literals of the spec scenarios, because the scenarios
/// quote them as executable assertions - both are rendered with the invariant culture and the
/// <c>0.####</c> pattern so a decimal(18,4) read from the database ("10.0000") reads as "10".
/// </remarks>
public sealed class ThreeWayMatchValidator
{
    /// <summary>
    /// Rejects a billing quantity above the remaining billable quantity of a receipt line.
    /// </summary>
    /// <param name="receivedQty">Quantity accepted by the purchase receipt line.</param>
    /// <param name="previouslyBilledQty">Quantity already billed by previous invoices on that line.</param>
    /// <param name="attemptingToBillQty">Quantity the invoice under validation wants to bill.</param>
    /// <exception cref="OverbillingNotAllowedException">
    /// spec BY-03 (nothing billed yet): <c>Cannot bill {attempting} units. Maximum receivable: {allowable}</c>.
    /// spec BY-06 (a previous bill consumed part of the receipt): <c>Only {allowable} units remaining to bill, {attempting} requested</c>.
    /// </exception>
    public static void ValidateBillingQuantity(decimal receivedQty, decimal previouslyBilledQty, decimal attemptingToBillQty)
    {
        var allowableQty = receivedQty - previouslyBilledQty;

        if (attemptingToBillQty <= allowableQty)
        {
            return;
        }

        var attempting = attemptingToBillQty.ToString("0.####", CultureInfo.InvariantCulture);
        var allowable = allowableQty.ToString("0.####", CultureInfo.InvariantCulture);

        throw new OverbillingNotAllowedException(
            previouslyBilledQty == 0m
                // spec BY-03: the first bill on the receipt - the ceiling is the received quantity.
                ? $"Cannot bill {attempting} units. Maximum receivable: {allowable}"
                // spec BY-06: partial billing already happened - only the remainder is billable.
                : $"Only {allowable} units remaining to bill, {attempting} requested");
    }
}
