using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Services;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 3.2 / D12: the FIFO valuation engine (pure Domain) against the numbers of spec §4 ST-02:
/// 50 units @ $10.00 + 10 units @ $12.00, issue 60 -> cost of goods sold = $620.00.
/// </summary>
public sealed class FifoValuationTests
{
    private static readonly DateOnly Day1 = new(2026, 1, 1);
    private static readonly DateOnly Day2 = new(2026, 1, 2);
    private static readonly DateOnly Day3 = new(2026, 1, 3);
    private static readonly DateOnly Day4 = new(2026, 1, 4);

    private static StockLedgerEntry Receipt(decimal qty, decimal rate, DateOnly postingDate, int minute) =>
        new()
        {
            Id = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            WarehouseId = Guid.NewGuid(),
            PostingDate = postingDate,
            QtyChange = qty,
            ValuationRate = rate,
            Amount = qty * rate,
            CreatedAt = new DateTimeOffset(postingDate, new TimeOnly(0, minute), TimeSpan.Zero),
        };

    private static StockLedgerEntry IssueOut(decimal qtyChange, decimal rate, DateOnly postingDate, int minute) =>
        new()
        {
            Id = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            WarehouseId = Guid.NewGuid(),
            PostingDate = postingDate,
            QtyChange = qtyChange,
            ValuationRate = rate,
            Amount = qtyChange * rate,
            CreatedAt = new DateTimeOffset(postingDate, new TimeOnly(0, minute), TimeSpan.Zero),
        };

    // ------------------------------------------------------------------ BuildLayers

    [Fact]
    public void BuildLayers_ReplaysReceiptsChronologically_AndKeepsOpenLayers()
    {
        // Deliberately seeded out of order: the engine must sort by (PostingDate, CreatedAt).
        var ledger = new[]
        {
            Receipt(10m, 12m, Day2, 0),
            Receipt(50m, 10m, Day1, 0),
        };

        var layers = FifoValuation.BuildLayers(ledger);

        Assert.Equal(2, layers.Count);
        Assert.Equal(50m, layers[0].RemainingQty);
        Assert.Equal(10m, layers[0].Rate);
        Assert.Equal(10m, layers[1].RemainingQty);
        Assert.Equal(12m, layers[1].Rate);
    }

    [Fact]
    public void BuildLayers_IssueConsumesOldestLayerFirst()
    {
        var ledger = new[]
        {
            Receipt(50m, 10m, Day1, 0),
            Receipt(10m, 12m, Day2, 0),
            new StockLedgerEntry
            {
                Id = Guid.NewGuid(),
                ItemId = Guid.NewGuid(),
                WarehouseId = Guid.NewGuid(),
                PostingDate = Day3,
                QtyChange = -60m,
                ValuationRate = 10.333333m,
                Amount = -620m,
                CreatedAt = new DateTimeOffset(Day3, new TimeOnly(0, 0), TimeSpan.Zero),
            },
        };

        var layers = FifoValuation.BuildLayers(ledger);

        // 50 @ $10 gone, 10 @ $12 gone -> nothing left.
        Assert.Empty(layers);
    }

    /// <summary>
    /// Task 3.9: an overdraw (a historical period of negative stock, or the pre-fix concurrency
    /// bug) must NOT be dropped from the replay - it survives as a debt layer so that
    /// sum(layers) always equals the Kardex net the API reports as on-hand.
    /// </summary>
    [Fact]
    public void BuildLayers_OverdrawnIssue_KeepsTheShortfallAsADebtLayer()
    {
        var ledger = new[]
        {
            Receipt(50m, 10m, Day1, 0),
            IssueOut(-70m, 11m, Day3, 0),
        };

        var layers = FifoValuation.BuildLayers(ledger);

        var debt = Assert.Single(layers);
        Assert.Equal(-20m, debt.RemainingQty);

        // The invariant Task 3.9's guard relies on: layers reflect the books exactly (50 - 70).
        Assert.Equal(-20m, layers.Sum(layer => layer.RemainingQty));
    }

    /// <summary>
    /// Task 3.9: with a debt layer present, <see cref="FifoValuation.Consume"/> must compare the
    /// request against the Kardex net (80), not against the sum of the open layers (100) -
    /// otherwise AllowNegativeStock=false would approve issues the books cannot cover.
    /// </summary>
    [Fact]
    public void Consume_WithDebtLayer_GuardsAgainstTheKardexNetNotTheOpenLayers()
    {
        var ledger = new[]
        {
            Receipt(50m, 10m, Day1, 0),
            IssueOut(-70m, 11m, Day3, 0),
            Receipt(100m, 12m, Day4, 0),
        };

        var layers = FifoValuation.BuildLayers(ledger);
        Assert.Equal(80m, layers.Sum(layer => layer.RemainingQty)); // 50 - 70 + 100

        // 90 requested against a net of 80 -> rejected with the TRUTHFUL available quantity.
        var ex = Assert.Throws<InsufficientStockException>(
            () => FifoValuation.Consume(layers, requestedQty: 90m, allowNegativeStock: false, "IT-001", "WH-01"));
        Assert.Equal(StockErrorCodes.InsufficientStock, ex.Code);
        Assert.Equal(80m, ex.Available);

        // Exactly the net consumes only from the open layer, never from the debt.
        var result = FifoValuation.Consume(layers, requestedQty: 80m, allowNegativeStock: false);
        Assert.Equal(960.00m, result.TotalCost); // 80 @ $12
        Assert.Equal(0m, result.ShortfallQty);
    }

    /// <summary>
    /// Successive overdraws accumulate the debt instead of growing <c>toConsume</c> unbounded.
    /// </summary>
    [Fact]
    public void BuildLayers_ConsecutiveOverdraws_AccumulateTheDebt()
    {
        var ledger = new[]
        {
            IssueOut(-20m, 10m, Day1, 0),
            IssueOut(-10m, 10m, Day2, 0),
        };

        var layers = FifoValuation.BuildLayers(ledger);

        Assert.Equal(-30m, layers.Sum(layer => layer.RemainingQty));
    }

    // ---------------------------------------------------------------------------- Consume

    [Fact]
    public void Consume_St02Scenario_CostsExactly620()
    {
        // spec §4 ST-02 verbatim: FIFO 50 @$10.00 + 10 @$12.00, issue 60 -> $620.00.
        var layers = new List<FifoLayer>
        {
            new(50m, 10m),
            new(10m, 12m),
        };

        var result = FifoValuation.Consume(layers, requestedQty: 60m, allowNegativeStock: false, "IT-001", "WH-01");

        Assert.Equal(620.00m, result.TotalCost);
        Assert.Equal(0m, result.ShortfallQty);
        Assert.Equal(2, result.Consumed.Count);
        Assert.Equal(50m, result.Consumed[0].Qty);
        Assert.Equal(10m, result.Consumed[0].Rate);
        Assert.Equal(500.00m, result.Consumed[0].Amount);
        Assert.Equal(10m, result.Consumed[1].Qty);
        Assert.Equal(12m, result.Consumed[1].Rate);
        Assert.Equal(120.00m, result.Consumed[1].Amount);

        // ValuationRate stamped on the outgoing Kardex row: 620 / 60 rounded to 6 decimals.
        Assert.Equal(10.333333m, result.AverageRate);
    }

    [Fact]
    public void Consume_PartialLayerConsumption_LeavesTheRemainderOpen()
    {
        var layers = new List<FifoLayer> { new(50m, 10m), new(10m, 12m) };

        var result = FifoValuation.Consume(layers, requestedQty: 55m, allowNegativeStock: false);

        Assert.Equal(560m, result.TotalCost); // 50@$10 + 5@$12
        Assert.Equal(2, result.Consumed.Count);
        Assert.Equal(5m, result.Consumed[1].Qty);
        Assert.Equal(0m, result.ShortfallQty);

        // The input list must be untouched (pure function): the caller keeps its layers.
        Assert.Equal(50m, layers[0].RemainingQty);
        Assert.Equal(10m, layers[1].RemainingQty);
    }

    [Fact]
    public void Consume_ForbiddenNegativeStock_ThrowsInsufficientStockException()
    {
        var layers = new List<FifoLayer> { new(30m, 10m) };

        var ex = Assert.Throws<InsufficientStockException>(
            () => FifoValuation.Consume(layers, requestedQty: 40m, allowNegativeStock: false, "IT-001", "WH-01"));

        Assert.Equal(StockErrorCodes.InsufficientStock, ex.Code);
        Assert.Equal("IT-001", ex.ItemCode);
        Assert.Equal("WH-01", ex.WarehouseCode);
        Assert.Equal(30m, ex.Available);
        Assert.Equal(40m, ex.Requested);
        Assert.Contains("AllowNegativeStock", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Consume_AllowedNegativeStock_ValuesTheShortfallAtTheMostRecentRate()
    {
        var layers = new List<FifoLayer> { new(30m, 10m), new(5m, 12m) };

        var result = FifoValuation.Consume(layers, requestedQty: 40m, allowNegativeStock: true, "IT-001", "WH-01");

        // 30 + 5 on hand -> 5 units short.
        Assert.Equal(5m, result.ShortfallQty);
        // 30@$10 + 5@$12 + 5@$12 (last layer rate) = 300 + 60 + 60 = 420.00
        Assert.Equal(420.00m, result.TotalCost);
        Assert.Equal(3, result.Consumed.Count);
        Assert.Equal(60.00m, result.Consumed[2].Amount);
    }

    [Fact]
    public void Consume_AllowedNegativeStock_WithNoLayersAtAll_ValuesTheShortfallAtZero()
    {
        var result = FifoValuation.Consume(
            new List<FifoLayer>(), requestedQty: 7m, allowNegativeStock: true, "IT-001", "WH-01");

        Assert.Equal(7m, result.ShortfallQty);
        Assert.Equal(0m, result.TotalCost);
        Assert.Equal(0m, result.AverageRate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Consume_NonPositiveQuantity_ThrowsArgumentException(decimal qty)
    {
        Assert.Throws<ArgumentException>(() => FifoValuation.Consume(new List<FifoLayer>(), qty, allowNegativeStock: true));
    }

    [Fact]
    public void Consume_NullLayers_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FifoValuation.Consume(null!, 1m, allowNegativeStock: true));
    }

    [Fact]
    public void BuildLayers_NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FifoValuation.BuildLayers(null!));
    }

    [Fact]
    public void BuildLayers_ZeroQuantityRows_AreIgnored()
    {
        var zero = Receipt(0m, 99m, Day1, 5);

        var layers = FifoValuation.BuildLayers(new[] { zero });

        Assert.Empty(layers);
    }
}

