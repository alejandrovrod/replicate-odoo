using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure C# field rules for stock voucher lines (Task 3.2). Kept separate from the posting engine
/// so quantities and rates can be rejected before any repository or transaction is touched.
/// </summary>
public static class StockEntryValidator
{
    /// <summary>A voucher must carry at least one line.</summary>
    /// <exception cref="StockValidationException">The line list is empty.</exception>
    public static void EnsureHasLines(IReadOnlyCollection<object>? lines)
    {
        if (lines is null || lines.Count == 0)
        {
            throw new StockValidationException(
                StockErrorCodes.NoLines,
                "A stock entry must contain at least one line.");
        }
    }

    /// <summary>
    /// Line rules: quantity is strictly positive (decimal(18,4)); receipts additionally require a
    /// user-supplied rate &gt; 0 because it defines the inventory valuation (spec ST-01).
    /// Issues and transfers take their rate from the FIFO engine instead, so no rate is accepted.
    /// </summary>
    /// <exception cref="StockValidationException">An invariant was violated.</exception>
    public static void EnsureValidLine(StockEntryType entryType, decimal qty, decimal? rate)
    {
        if (qty <= 0)
        {
            throw new StockValidationException(
                StockErrorCodes.InvalidQuantity,
                $"Line quantity must be greater than zero (received {qty:0.####}).");
        }

        if (entryType == StockEntryType.MaterialReceipt)
        {
            if (rate is not { } receiptRate || receiptRate <= 0)
            {
                throw new StockValidationException(
                    StockErrorCodes.InvalidRate,
                    "A material receipt requires a unit rate greater than zero (it values the incoming stock).");
            }
        }
    }
}
