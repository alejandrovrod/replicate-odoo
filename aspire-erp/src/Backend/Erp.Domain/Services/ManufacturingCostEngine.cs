namespace Erp.Domain.Services;

/// <summary>One BOM component line reduced to its costing inputs: consumed quantity, unit rate, scrap share.</summary>
/// <param name="Quantity">Consumed quantity; must be strictly positive.</param>
/// <param name="ValuationRate">Unit valuation rate; must be &gt;= 0.</param>
/// <param name="ScrapPercentage">Scrap share in percent; must be &gt;= 0.</param>
public sealed record BomCostItem(decimal Quantity, decimal ValuationRate, decimal ScrapPercentage);

/// <summary>One BOM workstation operation reduced to its costing inputs.</summary>
/// <param name="DurationMinutes">Step duration in minutes; must be strictly positive.</param>
/// <param name="HourRateTotal">Workstation composite hourly rate (passed as a value); must be &gt;= 0.</param>
public sealed record BomCostOperation(decimal DurationMinutes, decimal HourRateTotal);

/// <summary>
/// Manufacturing cost capitalization engine (spec invariant MF-01, plan.md §3): absorbs raw
/// materials, direct machine/labor hours and scrap salvage into the finished unit valuation rate.
/// Pure Domain (no EF, no NuGet - Constitution Article I.2), unit-tested without a database.
/// </summary>
public sealed class ManufacturingCostEngine
{
    public static decimal CalculateFinishedUnitCost(
        decimal totalRawMaterialCost,
        decimal totalOperatingCost,
        decimal scrapSalvageValue,
        decimal producedQuantity)
    {
        if (producedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(producedQuantity), "Produced quantity must be positive.");
        }

        var netTotalCost = totalRawMaterialCost + totalOperatingCost - scrapSalvageValue;
        return Math.Round(netTotalCost / producedQuantity, 4);
    }

    /// <summary>
    /// BOM cost roll-up (Task 9.2): per-line amount = qty x rate, scrap value = amount x scrap% ,
    /// operation cost = (minutes / 60) x hourly rate. Returns the four persisted snapshots
    /// (raw, operating, scrap, total) with total = raw + operating - scrap (spec MF-01).
    /// Line amounts round to 4 decimals (the GL amount scale, same as the FIFO slices).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A quantity, rate, scrap or duration input is out of range.</exception>
    public static (decimal RawMaterialCost, decimal OperatingCost, decimal ScrapCost, decimal TotalCost) CalculateBomTotals(
        IEnumerable<BomCostItem> items,
        IEnumerable<BomCostOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(operations);

        var rawMaterialCost = 0m;
        var scrapCost = 0m;

        foreach (var item in items)
        {
            if (item.Quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(items), $"BOM item quantity must be positive (received {item.Quantity}).");
            }

            if (item.ValuationRate < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(items), $"BOM item valuation rate must not be negative (received {item.ValuationRate}).");
            }

            if (item.ScrapPercentage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(items), $"BOM item scrap percentage must not be negative (received {item.ScrapPercentage}).");
            }

            var amount = Math.Round(item.Quantity * item.ValuationRate, 4, MidpointRounding.AwayFromZero);
            rawMaterialCost += amount;
            scrapCost += Math.Round(amount * item.ScrapPercentage / 100m, 4, MidpointRounding.AwayFromZero);
        }

        var operatingCost = 0m;

        foreach (var operation in operations)
        {
            if (operation.DurationMinutes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(operations), $"BOM operation duration must be positive (received {operation.DurationMinutes}).");
            }

            if (operation.HourRateTotal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(operations), $"BOM operation hourly rate must not be negative (received {operation.HourRateTotal}).");
            }

            operatingCost += Math.Round(
                operation.DurationMinutes / 60m * operation.HourRateTotal,
                4,
                MidpointRounding.AwayFromZero);
        }

        rawMaterialCost = Math.Round(rawMaterialCost, 4, MidpointRounding.AwayFromZero);
        operatingCost = Math.Round(operatingCost, 4, MidpointRounding.AwayFromZero);
        scrapCost = Math.Round(scrapCost, 4, MidpointRounding.AwayFromZero);
        var totalCost = Math.Round(rawMaterialCost + operatingCost - scrapCost, 4, MidpointRounding.AwayFromZero);

        return (rawMaterialCost, operatingCost, scrapCost, totalCost);
    }
}
