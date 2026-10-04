using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Task 9.3: the work-order state machine - the only legal arcs are Draft -&gt; Submitted -&gt;
/// InProcess -&gt; Completed plus Cancelled from any non-terminal state for the Block C (9.6)
/// reversal; every other move throws <c>invalid_status_transition</c>.
/// </summary>
public sealed class WorkOrderTests
{
    private static WorkOrder NewOrder(WorkOrderStatus status = WorkOrderStatus.Draft) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            OrderNumber = "WO-2026-00001",
            ProductionItemId = Guid.NewGuid(),
            BomId = Guid.NewGuid(),
            QuantityToProduce = 10m,
            Status = status,
            SourceWarehouseId = Guid.NewGuid(),
            WipWarehouseId = Guid.NewGuid(),
            TargetWarehouseId = Guid.NewGuid(),
            PlannedStartDate = new DateOnly(2026, 4, 1),
            PlannedEndDate = new DateOnly(2026, 4, 10),
        };

    [Fact]
    public void Submit_Draft_TransitionsToSubmitted()
    {
        var order = NewOrder();

        order.Submit();

        Assert.Equal(WorkOrderStatus.Submitted, order.Status);
    }

    [Fact]
    public void StartProduction_Submitted_TransitionsToInProcess()
    {
        var order = NewOrder(WorkOrderStatus.Submitted);

        order.StartProduction();

        Assert.Equal(WorkOrderStatus.InProcess, order.Status);
    }

    [Fact]
    public void Complete_InProcess_TransitionsToCompleted()
    {
        var order = NewOrder(WorkOrderStatus.InProcess);

        order.Complete();

        Assert.Equal(WorkOrderStatus.Completed, order.Status);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Submitted)]
    [InlineData(WorkOrderStatus.InProcess)]
    [InlineData(WorkOrderStatus.Completed)]
    [InlineData(WorkOrderStatus.Cancelled)]
    public void Submit_NonDraft_ThrowsInvalidStatusTransition(WorkOrderStatus status)
    {
        var order = NewOrder(status);

        var ex = Assert.Throws<ManufacturingValidationException>(() => order.Submit());
        Assert.Equal(ManufacturingErrorCodes.InvalidStatusTransition, ex.Code);
        Assert.Equal(status, order.Status);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Draft)]
    [InlineData(WorkOrderStatus.InProcess)]
    [InlineData(WorkOrderStatus.Completed)]
    [InlineData(WorkOrderStatus.Cancelled)]
    public void StartProduction_NonSubmitted_ThrowsInvalidStatusTransition(WorkOrderStatus status)
    {
        var order = NewOrder(status);

        var ex = Assert.Throws<ManufacturingValidationException>(() => order.StartProduction());
        Assert.Equal(ManufacturingErrorCodes.InvalidStatusTransition, ex.Code);
        Assert.Equal(status, order.Status);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Draft)]
    [InlineData(WorkOrderStatus.Submitted)]
    [InlineData(WorkOrderStatus.Completed)]
    [InlineData(WorkOrderStatus.Cancelled)]
    public void Complete_NonInProcess_ThrowsInvalidStatusTransition(WorkOrderStatus status)
    {
        var order = NewOrder(status);

        var ex = Assert.Throws<ManufacturingValidationException>(() => order.Complete());
        Assert.Equal(ManufacturingErrorCodes.InvalidStatusTransition, ex.Code);
        Assert.Equal(status, order.Status);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Draft)]
    [InlineData(WorkOrderStatus.Submitted)]
    [InlineData(WorkOrderStatus.InProcess)]
    public void Cancel_NonTerminal_TransitionsToCancelled(WorkOrderStatus status)
    {
        var order = NewOrder(status);

        order.Cancel();

        Assert.Equal(WorkOrderStatus.Cancelled, order.Status);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Completed)]
    [InlineData(WorkOrderStatus.Cancelled)]
    public void Cancel_Terminal_ThrowsInvalidStatusTransition(WorkOrderStatus status)
    {
        var order = NewOrder(status);

        var ex = Assert.Throws<ManufacturingValidationException>(() => order.Cancel());
        Assert.Equal(ManufacturingErrorCodes.InvalidStatusTransition, ex.Code);
        Assert.Equal(status, order.Status);
    }

    [Fact]
    public void Validator_ZeroQuantity_ThrowsInvalidWorkOrderQuantity()
    {
        var ex = Assert.Throws<ManufacturingValidationException>(() => WorkOrderValidator.EnsureValidQuantity(0m));
        Assert.Equal(ManufacturingErrorCodes.InvalidWorkOrderQuantity, ex.Code);
    }

    [Fact]
    public void Validator_EndBeforeStart_ThrowsInvalidWorkOrderDates()
    {
        var ex = Assert.Throws<ManufacturingValidationException>(
            () => WorkOrderValidator.EnsureValidDates(new DateOnly(2026, 4, 10), new DateOnly(2026, 4, 1)));
        Assert.Equal(ManufacturingErrorCodes.InvalidWorkOrderDates, ex.Code);
    }

    [Fact]
    public void Validator_SourceEqualsWip_ThrowsInvalidWorkOrderWarehouses()
    {
        var warehouse = Guid.NewGuid();
        var ex = Assert.Throws<ManufacturingValidationException>(
            () => WorkOrderValidator.EnsureValidWarehouses(warehouse, warehouse, Guid.NewGuid()));
        Assert.Equal(ManufacturingErrorCodes.InvalidWorkOrderWarehouses, ex.Code);
    }
}
