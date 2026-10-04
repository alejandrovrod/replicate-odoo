using Erp.Application.Features.Manufacturing.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 9.3: the work-order create/submit workflow exercised through the CQRS handlers against
/// in-memory repository doubles - the gapless WO voucher, the Task 9.3 acceptance gate (BOM must
/// be active AND default), the field guards, and the guarantee that every rejection writes ZERO
/// rows and leaves the order untouched.
/// </summary>
public sealed class WorkOrderWorkflowTests
{
    private static readonly DateOnly Start = new(2026, 4, 1);
    private static readonly DateOnly End = new(2026, 4, 10);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _otherCompanyId = Guid.NewGuid();
    private readonly Guid _fgItemId = Guid.NewGuid();
    private readonly Guid _bomId = Guid.NewGuid();
    private readonly Guid _sourceId = Guid.NewGuid();
    private readonly Guid _wipId = Guid.NewGuid();
    private readonly Guid _targetId = Guid.NewGuid();

    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeItemRepository _items = new();
    private readonly FakeWarehouseRepository _warehouses = new();
    private readonly FakeManufacturingRepository _manufacturing = new();

    public WorkOrderWorkflowTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = Guid.NewGuid(),
            Name = "Acme Industrial",
        };

        _items.Seed(new Item
        {
            Id = _fgItemId,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            ItemCode = "FG-001",
            ItemName = "Finished Assembly",
            ValuationMethod = ValuationMethod.Fifo,
        });

        _warehouses.Seed(
            NewWarehouse(_sourceId, "STORES"),
            NewWarehouse(_wipId, "WIP"),
            NewWarehouse(_targetId, "FG"));

        _manufacturing.SeedBom(new BillOfMaterials
        {
            Id = _bomId,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            BomNumber = "BOM-001",
            ItemId = _fgItemId,
            Quantity = 1m,
            IsActive = true,
            IsDefault = true,
        });
    }

    private Warehouse NewWarehouse(Guid id, string code) =>
        new()
        {
            Id = id,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            WarehouseCode = code,
            WarehouseName = code,
        };

    private CreateWorkOrderCommandHandler CreateHandler() =>
        new(_companies, _items, _warehouses, _manufacturing);

    private SubmitWorkOrderCommandHandler SubmitHandler() => new(_manufacturing);

    private CreateWorkOrderCommand ValidCreate() =>
        new(
            _companyId,
            _fgItemId,
            _bomId,
            QuantityToProduce: 10m,
            _sourceId,
            _wipId,
            _targetId,
            Start,
            End);

    private WorkOrder SeedOrder(WorkOrderStatus status = WorkOrderStatus.Draft, Guid? companyId = null)
    {
        var order = new WorkOrder
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = companyId ?? _companyId,
            OrderNumber = "WO-2026-00001",
            ProductionItemId = _fgItemId,
            BomId = _bomId,
            QuantityToProduce = 10m,
            Status = status,
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

    // ------------------------------------------------------------------ create (Draft + number)

    [Fact]
    public async Task Create_HappyPath_AssignsGaplessNumberAndDraft()
    {
        var result = await CreateHandler().HandleAsync(ValidCreate(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal("WO-2026-00001", dto.OrderNumber);
        Assert.Equal(WorkOrderStatus.Draft, dto.Status);
        Assert.Equal(10m, dto.QuantityToProduce);
        Assert.Equal(0m, dto.ProducedQuantity);
        Assert.Equal(1, _manufacturing.TransactionCount); // ONE transaction (Constitution III.4)
    }

    [Fact]
    public async Task Create_SecondOrder_IncrementsGaplessSequence()
    {
        await CreateHandler().HandleAsync(ValidCreate(), CancellationToken.None);
        var second = await CreateHandler().HandleAsync(ValidCreate(), CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Equal("WO-2026-00002", second.Value!.OrderNumber);
    }

    [Fact]
    public async Task Create_ZeroQuantity_FailsAndWritesNothing()
    {
        var result = await CreateHandler().HandleAsync(ValidCreate() with { QuantityToProduce = 0m }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.InvalidWorkOrderQuantity, result.Error!.Code);
        Assert.Empty(_manufacturing.WorkOrders);
    }

    [Fact]
    public async Task Create_EndBeforeStart_FailsAndWritesNothing()
    {
        var result = await CreateHandler().HandleAsync(
            ValidCreate() with { PlannedStartDate = End, PlannedEndDate = Start }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.InvalidWorkOrderDates, result.Error!.Code);
        Assert.Empty(_manufacturing.WorkOrders);
    }

    [Fact]
    public async Task Create_SourceEqualsWip_FailsAndWritesNothing()
    {
        var result = await CreateHandler().HandleAsync(
            ValidCreate() with { WipWarehouseId = _sourceId }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.InvalidWorkOrderWarehouses, result.Error!.Code);
        Assert.Empty(_manufacturing.WorkOrders);
    }

    [Fact]
    public async Task Create_WipEqualsTarget_FailsAndWritesNothing()
    {
        var result = await CreateHandler().HandleAsync(
            ValidCreate() with { TargetWarehouseId = _wipId }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.InvalidWorkOrderWarehouses, result.Error!.Code);
        Assert.Empty(_manufacturing.WorkOrders);
    }

    [Fact]
    public async Task Create_UnknownBom_FailsWithBomNotFoundAndWritesNothing()
    {
        var result = await CreateHandler().HandleAsync(
            ValidCreate() with { BomId = Guid.NewGuid() }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.BomNotFound, result.Error!.Code);
        Assert.Empty(_manufacturing.WorkOrders);
    }

    [Fact]
    public async Task Create_WarehouseOfAnotherCompany_FailsAndWritesNothing()
    {
        var foreign = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = _otherCompanyId,
            WarehouseCode = "FOREIGN",
            WarehouseName = "Foreign",
        };
        _warehouses.Seed(foreign);

        var result = await CreateHandler().HandleAsync(
            ValidCreate() with { SourceWarehouseId = foreign.Id }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.InvalidWorkOrderWarehouses, result.Error!.Code);
        Assert.Empty(_manufacturing.WorkOrders);
    }

    // ------------------------------------------------------------------ submit (acceptance gate)

    [Fact]
    public async Task Submit_HappyPath_DraftToSubmitted()
    {
        var order = SeedOrder();

        var result = await SubmitHandler().HandleAsync(
            new SubmitWorkOrderCommand(_companyId, order.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkOrderStatus.Submitted, result.Value!.Status);
        Assert.Equal(WorkOrderStatus.Submitted, order.Status);
    }

    [Fact]
    public async Task Submit_InactiveBom_FailsAndLeavesDraft()
    {
        var order = SeedOrder();
        var bomId = Guid.NewGuid();
        order.BomId = bomId;
        _manufacturing.SeedBom(new BillOfMaterials
        {
            Id = bomId,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            BomNumber = "BOM-OLD",
            ItemId = _fgItemId,
            Quantity = 1m,
            IsActive = false,
            IsDefault = true,
        });

        var result = await SubmitHandler().HandleAsync(
            new SubmitWorkOrderCommand(_companyId, order.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.InactiveBom, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.Draft, order.Status);
    }

    [Fact]
    public async Task Submit_NonDefaultBom_FailsAndLeavesDraft()
    {
        var order = SeedOrder();
        var bomId = Guid.NewGuid();
        order.BomId = bomId;
        _manufacturing.SeedBom(new BillOfMaterials
        {
            Id = bomId,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            BomNumber = "BOM-ALT",
            ItemId = _fgItemId,
            Quantity = 1m,
            IsActive = true,
            IsDefault = false,
        });

        var result = await SubmitHandler().HandleAsync(
            new SubmitWorkOrderCommand(_companyId, order.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.NonDefaultBom, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.Draft, order.Status);
    }

    [Fact]
    public async Task Submit_MissingBom_FailsWithBomNotFound()
    {
        var order = SeedOrder();
        order.BomId = Guid.NewGuid();

        var result = await SubmitHandler().HandleAsync(
            new SubmitWorkOrderCommand(_companyId, order.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.BomNotFound, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.Draft, order.Status);
    }

    [Fact]
    public async Task Submit_AlreadySubmitted_FailsWithInvalidStatusTransition()
    {
        var order = SeedOrder(WorkOrderStatus.Submitted);

        var result = await SubmitHandler().HandleAsync(
            new SubmitWorkOrderCommand(_companyId, order.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.Submitted, order.Status);
    }

    [Fact]
    public async Task Submit_UnknownOrder_FailsWithWorkOrderNotFound()
    {
        var result = await SubmitHandler().HandleAsync(
            new SubmitWorkOrderCommand(_companyId, Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.WorkOrderNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task Submit_OrderOfAnotherCompany_FailsWithoutMutatingIt()
    {
        var order = SeedOrder(companyId: _otherCompanyId);

        var result = await SubmitHandler().HandleAsync(
            new SubmitWorkOrderCommand(_companyId, order.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.WorkOrderNotFound, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.Draft, order.Status);
    }

    [Fact]
    public async Task Submit_ConcurrencyConflict_FailsWithConcurrencyConflict()
    {
        var order = SeedOrder();
        _manufacturing.FailNextWorkOrderUpdate = true;

        var result = await SubmitHandler().HandleAsync(
            new SubmitWorkOrderCommand(_companyId, order.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("concurrency_conflict", result.Error!.Code);
    }
}
