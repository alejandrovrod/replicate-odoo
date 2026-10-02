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
                var taken = Math.Min(layer.RemainingQty, toConsume);
                var left = layer.RemainingQty - taken;
                toConsume -= taken;
                layers[i] = layer with { RemainingQty = left };
            }

            // A negative balance (allowed by policy) simply leaves no layer - it is represented
            // by the absence of stock, not by a negative layer.
            layers.RemoveAll(layer => layer.RemainingQty <= 0);
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
    /// available quantity throws <see cref="InsufficientStockException"/> (Task 3.3); when true,
    /// the shortfall is valued at the most recent layer's rate (or 0.0000 when no layer exists).
    /// </param>
    /// <param name="itemCode">Item code for the exception message (context for operators).</param>
    /// <param name="warehouseCode">Warehouse code for the exception message.</param>
    /// <exception cref="InsufficientStockException">Policy forbids negative stock and layers run dry.</exception>
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

            // Shortfall valuation: most recent layer's rate, or 0.0000 when there is no layer at all.
            var shortfallRate = layers.Count > 0 ? layers[^1].Rate : 0m;
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
