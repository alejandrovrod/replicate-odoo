using Erp.Application.Features.Manufacturing.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 9.7: the transitive BOM-cycle closure enforced at work-order submit, exercised through
/// the CQRS handler against the in-memory repository - a multi-level A -&gt; B -&gt; A chain is
/// rejected with <c>circular_reference</c> and zero writes, while a diamond (shared
/// sub-component, no loop) and an acyclic chain submit cleanly. Inactive/non-default
/// intermediate recipes are correctly skipped: they cannot authorize production.
/// </summary>
public sealed class SubmitTransitiveCycleTests
{
    private static readonly DateOnly Start = new(2026, 4, 1);
    private static readonly DateOnly End = new(2026, 4, 10);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _sourceId = Guid.NewGuid();
    private readonly Guid _wipId = Guid.NewGuid();
    private readonly Guid _targetId = Guid.NewGuid();

    private readonly FakeManufacturingRepository _manufacturing = new();

    private SubmitWorkOrderCommandHandler Handler() => new(_manufacturing);

    private Guid SeedBom(Guid finishedItemId, Guid[] componentItemIds, bool isActive = true, bool isDefault = true)
    {
        var bomId = Guid.NewGuid();
        var bom = new BillOfMaterials
        {
            Id = bomId,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            BomNumber = $"BOM-{bomId:N}",
            ItemId = finishedItemId,
            Quantity = 1m,
            IsActive = isActive,
            IsDefault = isDefault,
        };

        foreach (var componentId in componentItemIds)
        {
            bom.Items.Add(new BomItem
            {
                Id = Guid.NewGuid(),
                ItemId = componentId,
                Quantity = 1m,
                ValuationRate = 10m,
                Amount = 10m,
            });
        }

        _manufacturing.SeedBom(bom);
        return bomId;
    }

    private WorkOrder SeedOrder(Guid bomId, Guid finishedItemId)
    {
        var order = new WorkOrder
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            OrderNumber = "WO-2026-00001",
            ProductionItemId = finishedItemId,
            BomId = bomId,
            QuantityToProduce = 10m,
            Status = WorkOrderStatus.Draft,
            SourceWarehouseId = _sourceId,
            WipWarehouseId = _wipId,
            TargetWarehouseId = _targetId,
            PlannedStartDate = Start,
            PlannedEndDate = End,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _manufacturing.SeedWorkOrder(order);
        return order;
    }

    [Fact]
    public async Task Submit_TransitiveCycleAToBToA_FailsWithCircularReferenceAndLeavesDraft()
    {
        var itemA = Guid.NewGuid();
        var itemB = Guid.NewGuid();
        var bomA = SeedBom(itemA, new[] { itemB });
        SeedBom(itemB, new[] { itemA });
        var order = SeedOrder(bomA, itemA);

        var result = await Handler().HandleAsync(
            new SubmitWorkOrderCommand(_companyId, order.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.CircularReference, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.Draft, order.Status);
    }

    [Fact]
    public async Task Submit_ThreeLevelCycle_FailsWithCircularReference()
    {
        var itemA = Guid.NewGuid();
        var itemB = Guid.NewGuid();
        var itemC = Guid.NewGuid();
        var bomA = SeedBom(itemA, new[] { itemB });
        SeedBom(itemB, new[] { itemC });
        SeedBom(itemC, new[] { itemA });
        var order = SeedOrder(bomA, itemA);

        var result = await Handler().HandleAsync(
            new SubmitWorkOrderCommand(_companyId, order.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.CircularReference, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.Draft, order.Status);
    }

    [Fact]
    public async Task Submit_SelfReferencingComponent_FailsWithCircularReference()
    {
        var itemA = Guid.NewGuid();
        var bomA = SeedBom(itemA, new[] { itemA });
        var order = SeedOrder(bomA, itemA);

        var result = await Handler().HandleAsync(
            new SubmitWorkOrderCommand(_companyId, order.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.CircularReference, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.Draft, order.Status);
    }

    /// <summary>
    /// The diamond: FG consumes B and C, both consume D, D is a leaf. D is reached twice
    /// through DIFFERENT paths but never repeats within one path - submission succeeds.
    /// </summary>
    [Fact]
    public async Task Submit_DiamondSharedSubComponent_SubmitsSuccessfully()
    {
        var itemFg = Guid.NewGuid();
        var itemB = Guid.NewGuid();
        var itemC = Guid.NewGuid();
        var itemD = Guid.NewGuid();
        var bomFg = SeedBom(itemFg, new[] { itemB, itemC });
        SeedBom(itemB, new[] { itemD });
        SeedBom(itemC, new[] { itemD });
        var order = SeedOrder(bomFg, itemFg);

        var result = await Handler().HandleAsync(
            new SubmitWorkOrderCommand(_companyId, order.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkOrderStatus.Submitted, order.Status);
    }

    [Fact]
    public async Task Submit_AcyclicChain_SubmitsSuccessfully()
    {
        var itemFg = Guid.NewGuid();
        var itemB = Guid.NewGuid();
        var itemC = Guid.NewGuid();
        var bomFg = SeedBom(itemFg, new[] { itemB });
        SeedBom(itemB, new[] { itemC });
        var order = SeedOrder(bomFg, itemFg);

        var result = await Handler().HandleAsync(
            new SubmitWorkOrderCommand(_companyId, order.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkOrderStatus.Submitted, order.Status);
    }

    /// <summary>
    /// B's only recipe looping back to FG is INACTIVE: the walk follows default ACTIVE BOMs
    /// only, so the dead recipe is skipped and submission succeeds.
    /// </summary>
    [Fact]
    public async Task Submit_CycleThroughInactiveBom_SubmitsSuccessfully()
    {
        var itemFg = Guid.NewGuid();
        var itemB = Guid.NewGuid();
        var bomFg = SeedBom(itemFg, new[] { itemB });
        SeedBom(itemB, new[] { itemFg }, isActive: false);
        var order = SeedOrder(bomFg, itemFg);

        var result = await Handler().HandleAsync(
            new SubmitWorkOrderCommand(_companyId, order.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkOrderStatus.Submitted, order.Status);
    }

    /// <summary>
    /// B's looping recipe is active but NOT the default: skipped for the same reason.
    /// </summary>
    [Fact]
    public async Task Submit_CycleThroughNonDefaultBom_SubmitsSuccessfully()
    {
        var itemFg = Guid.NewGuid();
        var itemB = Guid.NewGuid();
        var bomFg = SeedBom(itemFg, new[] { itemB });
        SeedBom(itemB, new[] { itemFg }, isActive: true, isDefault: false);
        var order = SeedOrder(bomFg, itemFg);

        var result = await Handler().HandleAsync(
            new SubmitWorkOrderCommand(_companyId, order.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkOrderStatus.Submitted, order.Status);
    }
}
