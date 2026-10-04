using Erp.Domain.Entities;
using Erp.Domain.Services;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Task 9.2 cost roll-up proofs: spec MF-01's own numbers must come out EXACT
/// (2xA@$15 + 1xB@$20 + 30min WS-01@$40/h -&gt; raw 50, operating 20, scrap 0, total 70),
/// scrap deducts from the total, and out-of-range inputs throw.
/// </summary>
public sealed class ManufacturingCostEngineTests
{
    private static readonly IReadOnlyList<BomCostItem> Mf01Items =
    [
        new BomCostItem(Quantity: 2m, ValuationRate: 15m, ScrapPercentage: 0m),
        new BomCostItem(Quantity: 1m, ValuationRate: 20m, ScrapPercentage: 0m),
    ];

    private static readonly IReadOnlyList<BomCostOperation> Mf01Operations =
    [
        new BomCostOperation(DurationMinutes: 30m, HourRateTotal: 40m),
    ];

    [Fact]
    public void CalculateFinishedUnitCost_Mf01Literal_ReturnsSeventy()
    {
        // Spec MF-01 verbatim: $50 materials + $20 operations, no scrap, 1 unit produced.
        var unitCost = ManufacturingCostEngine.CalculateFinishedUnitCost(
            totalRawMaterialCost: 50m,
            totalOperatingCost: 20m,
            scrapSalvageValue: 0m,
            producedQuantity: 1m);

        Assert.Equal(70m, unitCost);
    }

    [Fact]
    public void CalculateFinishedUnitCost_ScrapValue_DeductsFromNetCost()
    {
        var unitCost = ManufacturingCostEngine.CalculateFinishedUnitCost(
            totalRawMaterialCost: 50m,
            totalOperatingCost: 20m,
            scrapSalvageValue: 5m,
            producedQuantity: 1m);

        Assert.Equal(65m, unitCost);
    }

    [Fact]
    public void CalculateFinishedUnitCost_ZeroProducedQuantity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ManufacturingCostEngine.CalculateFinishedUnitCost(50m, 20m, 0m, 0m));
    }

    [Fact]
    public void CalculateFinishedUnitCost_NegativeProducedQuantity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ManufacturingCostEngine.CalculateFinishedUnitCost(50m, 20m, 0m, -10m));
    }

    [Fact]
    public void CalculateBomTotals_Mf01Literal_ReturnsExactSnapshots()
    {
        var (raw, operating, scrap, total) = ManufacturingCostEngine.CalculateBomTotals(Mf01Items, Mf01Operations);

        Assert.Equal(50m, raw);
        Assert.Equal(20m, operating);
        Assert.Equal(0m, scrap);
        Assert.Equal(70m, total);
    }

    [Fact]
    public void CalculateBomTotals_ScrapPercentage_DeductsSalvageValue()
    {
        // 10 units @ $10 with 10% scrap: raw 100, scrap 10, total 90.
        var items = new[] { new BomCostItem(Quantity: 10m, ValuationRate: 10m, ScrapPercentage: 10m) };

        var (raw, operating, scrap, total) = ManufacturingCostEngine.CalculateBomTotals(
            items,
            Array.Empty<BomCostOperation>());

        Assert.Equal(100m, raw);
        Assert.Equal(0m, operating);
        Assert.Equal(10m, scrap);
        Assert.Equal(90m, total);
    }

    [Fact]
    public void CalculateBomTotals_EmptyInputs_ReturnsZeros()
    {
        var (raw, operating, scrap, total) = ManufacturingCostEngine.CalculateBomTotals(
            Array.Empty<BomCostItem>(),
            Array.Empty<BomCostOperation>());

        Assert.Equal(0m, raw);
        Assert.Equal(0m, operating);
        Assert.Equal(0m, scrap);
        Assert.Equal(0m, total);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void CalculateBomTotals_NonPositiveItemQuantity_Throws(decimal quantity)
    {
        var items = new[] { new BomCostItem(Quantity: quantity, ValuationRate: 15m, ScrapPercentage: 0m) };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ManufacturingCostEngine.CalculateBomTotals(items, Mf01Operations));
    }

    [Fact]
    public void CalculateBomTotals_NegativeValuationRate_Throws()
    {
        var items = new[] { new BomCostItem(Quantity: 2m, ValuationRate: -15m, ScrapPercentage: 0m) };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ManufacturingCostEngine.CalculateBomTotals(items, Mf01Operations));
    }

    [Fact]
    public void CalculateBomTotals_NegativeScrapPercentage_Throws()
    {
        var items = new[] { new BomCostItem(Quantity: 2m, ValuationRate: 15m, ScrapPercentage: -5m) };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ManufacturingCostEngine.CalculateBomTotals(items, Mf01Operations));
    }

    [Fact]
    public void CalculateBomTotals_NonPositiveOperationDuration_Throws()
    {
        var operations = new[] { new BomCostOperation(DurationMinutes: 0m, HourRateTotal: 40m) };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ManufacturingCostEngine.CalculateBomTotals(Mf01Items, operations));
    }

    [Fact]
    public void CalculateBomTotals_NegativeOperationRate_Throws()
    {
        var operations = new[] { new BomCostOperation(DurationMinutes: 30m, HourRateTotal: -40m) };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ManufacturingCostEngine.CalculateBomTotals(Mf01Items, operations));
    }

    [Fact]
    public void CalculateOperationCost_ThirtyMinutesAtFortyPerHour_ReturnsTwenty()
    {
        Assert.Equal(20m, BomOperation.CalculateCost(durationMinutes: 30m, hourRateTotal: 40m));
    }

    [Fact]
    public void CalculateOperationCost_FullHourAtCompositeRate_ReturnsComposite()
    {
        // 25 + 10 + 5 composite over 60 minutes costs exactly the composite.
        Assert.Equal(40m, BomOperation.CalculateCost(durationMinutes: 60m, hourRateTotal: 40m));
    }

    [Fact]
    public void CalculateOperationCost_NonPositiveDuration_ThrowsWithTypedCode()
    {
        var ex = Assert.Throws<Exceptions.ManufacturingValidationException>(
            () => BomOperation.CalculateCost(durationMinutes: 0m, hourRateTotal: 40m));

        Assert.Equal(ManufacturingErrorCodes.InvalidOperationDuration, ex.Code);
    }

    [Fact]
    public void CalculateOperationCost_NegativeRate_ThrowsWithTypedCode()
    {
        var ex = Assert.Throws<Exceptions.ManufacturingValidationException>(
            () => BomOperation.CalculateCost(durationMinutes: 30m, hourRateTotal: -1m));

        Assert.Equal(ManufacturingErrorCodes.NegativeHourRate, ex.Code);
    }
}
