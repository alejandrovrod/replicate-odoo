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

    /// <summary>
    /// Batch-and-serial Fase 3 shape rules (no data access): a tracked item must name its
    /// batch (by id for issues/transfers, by number for receipts - exactly one of them), and
    /// a serialized item must list exactly as many serials as units move. Existence,
    /// expiry, warehouse and duplicate checks live in the tracking service, which owns repos.
    /// </summary>
    /// <exception cref="StockValidationException">An invariant was violated.</exception>
    public static IReadOnlyList<string> EnsureTrackingShape(
        string itemCode,
        bool hasBatchNo,
        Guid? batchId,
        string? batchNo,
        bool isReceipt,
        bool hasSerialNo,
        IReadOnlyList<string>? serials,
        decimal qty)
    {
        if (hasBatchNo)
        {
            var byId = batchId.HasValue && batchId.Value != Guid.Empty;
            var byNumber = !string.IsNullOrWhiteSpace(batchNo);
            if (!byId && !byNumber)
            {
                throw new StockValidationException(
                    StockErrorCodes.BatchRequired,
                    $"Item '{itemCode}' is batch-tracked: a line must name a batch "
                    + (isReceipt ? "(batch number). " : "(batch id)."));
            }

            if (byId && byNumber)
            {
                throw new StockValidationException(
                    StockErrorCodes.BatchRequired,
                    $"Item '{itemCode}': pass either a batch id or a batch number, not both.");
            }
        }

        if (!hasSerialNo)
        {
            return Array.Empty<string>();
        }

        var cleaned = (serials ?? Array.Empty<string>())
            .Select(s => (s ?? string.Empty).Trim())
            .Where(s => s.Length > 0)
            .ToList();

        if (cleaned.Count == 0)
        {
            throw new StockValidationException(
                StockErrorCodes.SerialRequired,
                $"Item '{itemCode}' is serial-tracked: a line must list the serial numbers of the moved units.");
        }

        var duplicate = cleaned
            .GroupBy(s => s, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new StockValidationException(
                StockErrorCodes.SerialAlreadyExists,
                $"Serial '{duplicate.Key}' is listed twice in the same line.");
        }

        // One serial per unit: fractional quantities can never match an integer count.
        if (cleaned.Count != (int)qty || qty != (int)qty)
        {
            throw new StockValidationException(
                StockErrorCodes.SerialCountMismatch,
                $"Item '{itemCode}' moves {qty:0.####} units but lists {cleaned.Count} serials: "
                + "one serial per unit is required.");
        }

        return cleaned;
    }
}
