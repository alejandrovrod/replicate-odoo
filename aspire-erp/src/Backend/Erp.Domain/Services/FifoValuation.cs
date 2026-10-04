using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Services;

/// <summary>One open FIFO cost layer: what is left of a receipt at a given unit rate.</summary>
/// <param name="RemainingQty">Units still available in this layer.</param>
/// <param name="Rate">Unit valuation rate of the layer.</param>
public sealed record FifoLayer(decimal RemainingQty, decimal Rate);

/// <summary>A slice of a layer consumed by an issue/transfer, with its 4-decimal value.</summary>
public sealed record FifoConsumedLayer(decimal Qty, decimal Rate, decimal Amount);

/// <summary>Outcome of a FIFO consumption request.</summary>
/// <param name="Consumed">Layers consumed, oldest first.</param>
/// <param name="TotalCost">Total cost of the consumption, rounded to 4 decimals (the GL amount).</param>
/// <param name="ShortfallQty">Units that no layer could cover (only when negative stock is allowed).</param>
/// <param name="AverageRate">
/// TotalCost / requested quantity rounded to 6 decimals - the ValuationRate stamped on the
/// outgoing StockLedgerEntry row.
/// </param>
public sealed record FifoResult(
    IReadOnlyList<FifoConsumedLayer> Consumed,
    decimal TotalCost,
    decimal ShortfallQty,
    decimal AverageRate);

/// <summary>
/// FIFO valuation as PURE DOMAIN LOGIC (decision D5): zero dependencies beyond BCL + Domain
/// entities (Constitution Article I.2), therefore unit-testable without EF or a database.
/// </summary>
/// <remarks>
/// Two responsibilities:
/// <list type="number">
/// <item><see cref="BuildLayers"/> replays the signed Kardex rows chronologically and rebuilds the
/// open cost layers (a receipt opens a layer, an issue consumes the OLDEST layer first).</item>
/// <item><see cref="Consume"/> takes those layers plus a requested quantity and returns the
/// consumed slices, their total cost, and - when the company policy allows it - the shortfall.</item>
/// </list>
/// Only FIFO is implemented in Phase 3 (spec ST-02); LIFO / Moving Average are deliberately NOT
/// half-built - <c>StockPostingService</c> rejects them with an explicit NotSupportedException.
/// </remarks>
public static class FifoValuation
{
    /// <summary>
    /// Replays signed StockLedgerEntry rows into the currently open cost layers.
    /// Rows are ordered by PostingDate, then CreatedAt (stable sort, so the repository's own
    /// Id tie-breaker is preserved for rows of the same voucher).
    /// </summary>
    public static IReadOnlyList<FifoLayer> BuildLayers(IEnumerable<StockLedgerEntry> ledgerEntries)
    {
        ArgumentNullException.ThrowIfNull(ledgerEntries);

        var ordered = ledgerEntries
            .OrderBy(entry => entry.PostingDate)
            .ThenBy(entry => entry.CreatedAt)
            .ToList();

        var layers = new List<FifoLayer>();

        foreach (var entry in ordered)
        {
            if (entry.QtyChange > 0)
            {
                layers.Add(new FifoLayer(entry.QtyChange, entry.ValuationRate));
                continue;
            }

            if (entry.QtyChange == 0)
            {
                continue;
            }

            // Issue/transfer out: consume the OLDEST open layer first (FIFO).
            var toConsume = -entry.QtyChange;
            for (var i = 0; i < layers.Count && toConsume > 0; i++)
            {
                var layer = layers[i];
                if (layer.RemainingQty <= 0)
                {
                    // An existing shortfall layer holds no units - skip it (otherwise subtracting
                    // a negative "taken" would grow toConsume instead of shrinking it).
                    continue;
                }

                var taken = Math.Min(layer.RemainingQty, toConsume);
                var left = layer.RemainingQty - taken;
                toConsume -= taken;
                layers[i] = layer with { RemainingQty = left };
            }

            // The overdrawn remainder is kept as a NEGATIVE layer (the stock debt) instead of
            // being dropped. Dropping it made sum(layers) exceed the Kardex net whenever the
            // history contained an overdraw (a negative-stock period or the pre-Task-3.9
            // concurrency bug), so Consume's AllowNegativeStock=false guard approved issues the
            // books could not cover: Task 3.9 requires "exactly available units are issued",
            // where available IS the Kardex net.
            if (toConsume > 0)
            {
                layers.Add(new FifoLayer(-toConsume, entry.ValuationRate));
            }

            layers.RemoveAll(layer => layer.RemainingQty == 0);
        }

        return layers;
    }

    /// <summary>
    /// Consumes <paramref name="requestedQty"/> units from <paramref name="layers"/> in FIFO order.
    /// </summary>
    /// <param name="layers">Open cost layers, oldest first (from <see cref="BuildLayers"/>).</param>
    /// <param name="requestedQty">Units to consume; must be strictly positive.</param>
    /// <param name="allowNegativeStock">
    /// Company policy (<c>Company.AllowNegativeStock</c>): when false, a request above the
    /// available quantity (the Kardex net: open layers minus any debt layer) throws
    /// <see cref="InsufficientStockException"/> (Task 3.3 / 3.9); when true, the shortfall is
    /// valued at the most recent open layer's rate (or 0.0000 when no open layer exists).
    /// </param>
    /// <param name="itemCode">Item code for the exception message (context for operators).</param>
    /// <param name="warehouseCode">Warehouse code for the exception message.</param>
    /// <exception cref="InsufficientStockException">
    /// Policy forbids negative stock and the request exceeds the available quantity.
    /// </exception>
    public static FifoResult Consume(
        IReadOnlyList<FifoLayer> layers,
        decimal requestedQty,
        bool allowNegativeStock,
        string itemCode = "",
        string warehouseCode = "")
    {
        ArgumentNullException.ThrowIfNull(layers);

        if (requestedQty <= 0)
        {
            throw new ArgumentException(
                $"The requested quantity must be greater than zero (received {requestedQty}).",
                nameof(requestedQty));
        }

        var available = 0m;
        foreach (var layer in layers)
        {
            available += layer.RemainingQty;
        }

        // The guard is the Kardex net (open layers MINUS any debt layer), which is exactly what
        // the API reports as on-hand: "exactly available units are issued" (Task 3.9). Checking
        // only whether the open layers run dry would let a request consume past an existing
        // debt and oversell the books.
        if (!allowNegativeStock && requestedQty > available)
        {
            throw new InsufficientStockException(itemCode, warehouseCode, available, requestedQty);
        }

        var consumed = new List<FifoConsumedLayer>();
        var remaining = requestedQty;
        var totalCost = 0m;

        foreach (var layer in layers)
        {
            if (remaining <= 0)
            {
                break;
            }

            var taken = Math.Min(layer.RemainingQty, remaining);
            if (taken <= 0)
            {
                continue;
            }

            var amount = Math.Round(taken * layer.Rate, 4, MidpointRounding.AwayFromZero);
            consumed.Add(new FifoConsumedLayer(taken, layer.Rate, amount));
            totalCost += amount;
            remaining -= taken;
        }

        var shortfall = 0m;
        if (remaining > 0)
        {
            if (!allowNegativeStock)
            {
                throw new InsufficientStockException(itemCode, warehouseCode, available, requestedQty);
            }

            // Shortfall valuation: most recent OPEN layer's rate, or 0.0000 when no open layer
            // exists (negative layers hold debt, not units, so they are not valuation sources).
            var shortfallRate = layers.LastOrDefault(layer => layer.RemainingQty > 0)?.Rate ?? 0m;
            var amount = Math.Round(remaining * shortfallRate, 4, MidpointRounding.AwayFromZero);
            consumed.Add(new FifoConsumedLayer(remaining, shortfallRate, amount));
            totalCost += amount;
            shortfall = remaining;
        }

        totalCost = Math.Round(totalCost, 4, MidpointRounding.AwayFromZero);
        var averageRate = Math.Round(totalCost / requestedQty, 6, MidpointRounding.AwayFromZero);

        return new FifoResult(consumed, totalCost, shortfall, averageRate);
    }
}
