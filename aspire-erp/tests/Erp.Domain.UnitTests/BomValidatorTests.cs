using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Task 9.2 validator proofs: a finished item cannot be a component of itself
/// (<c>circular_reference</c>), a BOM with zero items is rejected, and every
/// quantity/rate/amount/cost input is range-guarded with a typed code.
/// </summary>
public sealed class BomValidatorTests
{
    private static BomItem Component(Guid? itemId = null, decimal quantity = 1m) =>
        new()
        {
            Id = Guid.NewGuid(),
            BomId = Guid.NewGuid(),
            ItemId = itemId ?? Guid.NewGuid(),
            Quantity = quantity,
            UomId = Guid.NewGuid(),
            ValuationRate = 10m,
            Amount = 10m,
        };

    [Fact]
    public void EnsureNoSelfReference_FinishedItemIsOwnComponent_ThrowsCircularReference()
    {
        var finishedItemId = Guid.NewGuid();
        var items = new[] { Component(finishedItemId) };

        var ex = Assert.Throws<ManufacturingValidationException>(
            () => BomValidator.EnsureNoSelfReference(finishedItemId, items));

        Assert.Equal(ManufacturingErrorCodes.CircularReference, ex.Code);
        Assert.Equal("circular_reference", ex.Code);
    }

    [Fact]
    public void EnsureNoSelfReference_DistinctComponents_Passes()
    {
        var finishedItemId = Guid.NewGuid();
        var items = new[] { Component(), Component() };

        BomValidator.EnsureNoSelfReference(finishedItemId, items); // must not throw
    }

    [Fact]
    public void EnsureNoCycle_FinishedItemInAncestorChain_ThrowsCircularReference()
    {
        // Transitive plug-in point for Task 9.7: the ancestor chain already holds the item.
        var finishedItemId = Guid.NewGuid();
        var ancestors = new List<Guid> { Guid.NewGuid(), finishedItemId };

        var ex = Assert.Throws<ManufacturingValidationException>(
            () => BomValidator.EnsureNoCycle(finishedItemId, ancestors));

        Assert.Equal(ManufacturingErrorCodes.CircularReference, ex.Code);
    }

    [Fact]
    public void EnsureNoCycle_LoopingAncestorChain_ThrowsCircularReference()
    {
        var looped = Guid.NewGuid();
        var ancestors = new List<Guid> { looped, looped };

        Assert.Throws<ManufacturingValidationException>(
            () => BomValidator.EnsureNoCycle(Guid.NewGuid(), ancestors));
    }

    [Fact]
    public void EnsureNoCycle_AcyclicChain_Passes()
    {
        BomValidator.EnsureNoCycle(Guid.NewGuid(), new List<Guid> { Guid.NewGuid() }); // must not throw
    }

    [Fact]
    public void EnsureHasItems_EmptyBom_ThrowsWithTypedCode()
    {
        var ex = Assert.Throws<ManufacturingValidationException>(
            () => BomValidator.EnsureHasItems(Array.Empty<BomItem>()));

        Assert.Equal(ManufacturingErrorCodes.EmptyBom, ex.Code);
    }

    [Fact]
    public void EnsureHasItems_NonEmptyBom_Passes()
    {
        BomValidator.EnsureHasItems(new[] { Component() }); // must not throw
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EnsureValidQuantity_NonPositiveQuantity_ThrowsWithTypedCode(decimal quantity)
    {
        var ex = Assert.Throws<ManufacturingValidationException>(
            () => BomValidator.EnsureValidQuantity(quantity));

        Assert.Equal(ManufacturingErrorCodes.InvalidBomQuantity, ex.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void EnsureValidItem_NonPositiveQuantity_ThrowsWithTypedCode(decimal quantity)
    {
        var ex = Assert.Throws<ManufacturingValidationException>(
            () => BomValidator.EnsureValidItem(Component(quantity: quantity)));

        Assert.Equal(ManufacturingErrorCodes.InvalidBomItemQuantity, ex.Code);
    }

    [Fact]
    public void EnsureValidItem_NegativeValuationRate_ThrowsWithTypedCode()
    {
        var item = Component();
        item.ValuationRate = -0.5m;

        var ex = Assert.Throws<ManufacturingValidationException>(
            () => BomValidator.EnsureValidItem(item));

        Assert.Equal(ManufacturingErrorCodes.NegativeValuationRate, ex.Code);
    }

    [Fact]
    public void EnsureValidItem_NegativeAmount_ThrowsWithTypedCode()
    {
        var item = Component();
        item.Amount = -1m;

        var ex = Assert.Throws<ManufacturingValidationException>(
            () => BomValidator.EnsureValidItem(item));

        Assert.Equal(ManufacturingErrorCodes.NegativeBomAmount, ex.Code);
    }

    [Fact]
    public void EnsureValidItem_NegativeScrapPercentage_ThrowsWithTypedCode()
    {
        var item = Component();
        item.ScrapPercentage = -5m;

        var ex = Assert.Throws<ManufacturingValidationException>(
            () => BomValidator.EnsureValidItem(item));

        Assert.Equal(ManufacturingErrorCodes.NegativeScrapPercentage, ex.Code);
    }

    [Fact]
    public void EnsureValidItem_ValidLine_Passes()
    {
        BomValidator.EnsureValidItem(Component()); // must not throw
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void EnsureValidOperation_NonPositiveDuration_ThrowsWithTypedCode(decimal minutes)
    {
        var operation = new BomOperation
        {
            Id = Guid.NewGuid(),
            BomId = Guid.NewGuid(),
            WorkstationId = Guid.NewGuid(),
            DurationMinutes = minutes,
        };

        var ex = Assert.Throws<ManufacturingValidationException>(
            () => BomValidator.EnsureValidOperation(operation));

        Assert.Equal(ManufacturingErrorCodes.InvalidOperationDuration, ex.Code);
    }

    [Fact]
    public void BillOfMaterials_Defaults_MatchSpecMf01()
    {
        // Scenario MF-01: a submitted BOM is active and default.
        var bom = new BillOfMaterials();

        Assert.True(bom.IsActive);
        Assert.True(bom.IsDefault);
        Assert.Equal(1m, bom.Quantity);
        Assert.Equal(string.Empty, bom.BomNumber);
    }
}
