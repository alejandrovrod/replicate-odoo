using Erp.Application.Features.Buying.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 4.1 acceptance: the purchase order workflow (Draft -&gt; Submitted -&gt; PartiallyReceived -&gt; Completed)
/// exercised through the CQRS handlers - gapless PO-YYYY-NNNNN vouchers, supplier existence/activity
/// rules and the invalid_status_transition failures that the API maps to 409 - against in-memory
/// repository doubles (Constitution I.2/I.3).
/// </summary>
public sealed class PurchaseOrderWorkflowTests
{
    private static readonly DateOnly PostingDate = new(2026, 3, 2);

    private readonly Guid _companyId = Guid.NewGuid();

    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeSupplierRepository _suppliers = new();
    private readonly FakeItemRepository _items = new();
    private readonly FakePurchaseRepository _purchases = new();

    private readonly Supplier _supplier;
    private readonly Item _item;

    public PurchaseOrderWorkflowTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = Guid.NewGuid(),
            Name = "Acme Industrial",
        };

        _supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Code = "SUP-001",
            Name = "Acme Industrial Supplies",
            IsActive = true,
        };
        _suppliers.Seed(_supplier);

        _item = new Item
        {
            Id = Guid.NewGuid(),
            ItemCode = "IT-001",
            ItemName = "Steel Bracket",
            ValuationMethod = ValuationMethod.Fifo,
            StockUomId = Guid.NewGuid(),
        };
        _items.Seed(_item);
    }

    private CreatePurchaseOrderCommandHandler CreateOrderHandler() =>
        new(_companies, _suppliers, _items, _purchases);

    private SubmitPurchaseOrderCommandHandler SubmitOrderHandler() =>
        new(_suppliers, _items, _purchases);

    private CreatePurchaseOrderCommand NewOrder() =>
        new(_companyId, _supplier.Id, PostingDate, PostingDate,
            new[] { new CreatePurchaseOrderItem(_item.Id, 10m, 100m) });

    private PurchaseOrder SeedOrder(PurchaseOrderStatus status, Guid? companyId = null)
    {
        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId ?? _companyId,
            SupplierId = _supplier.Id,
            Status = status,
            TransactionDate = PostingDate,
            OrderNumber = "PO-2026-00001",
            CreatedAt = DateTimeOffset.UtcNow,
            Items = new List<PurchaseOrderItem>
            {
                new() { Id = Guid.NewGuid(), ItemId = _item.Id, Quantity = 10m, Rate = 100m, LineNumber = 1 },
            },
        };
        _purchases.SeedOrder(order);
        return order;
    }

    // ------------------------------------------------------------------------- create (draft)

    [Fact]
    public async Task Create_ValidLines_PersistsDraftWithGaplessVoucher()
    {
        var first = await CreateOrderHandler().HandleAsync(NewOrder());
        var second = await CreateOrderHandler().HandleAsync(NewOrder());

        Assert.True(first.IsSuccess);
        Assert.NotNull(first.Value);
        Assert.Equal("PO-2026-00001", first.Value!.OrderNumber);
        Assert.Equal(PurchaseOrderStatus.Draft, first.Value.Status);
        Assert.Equal("PO-2026-00002", second.Value!.OrderNumber);
        Assert.Equal(PurchaseOrderStatus.Draft, second.Value.Status);

        Assert.Equal(2, _purchases.Orders.Count);
        var saved = _purchases.Orders[0];
        Assert.Equal(_supplier.Id, saved.SupplierId);
        var line = Assert.Single(saved.Items);
        Assert.Equal(_item.Id, line.ItemId);
        Assert.Equal(10m, line.Quantity);
        Assert.Equal(100m, line.Rate);
        Assert.Equal(1, line.LineNumber);

        // One number+insert transaction per creation (Constitution III.4).
        Assert.Equal(2, _purchases.TransactionCount);
    }

    [Fact]
    public async Task Create_UnknownSupplier_FailsWithSupplierNotFound()
    {
        var result = await CreateOrderHandler().HandleAsync(
            new CreatePurchaseOrderCommand(_companyId, Guid.NewGuid(), PostingDate, PostingDate,
                new[] { new CreatePurchaseOrderItem(_item.Id, 10m, 100m) }));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(PurchaseErrorCodes.SupplierNotFound, result.Error!.Code);
        Assert.Empty(_purchases.Orders);
    }

    [Fact]
    public async Task Create_InactiveSupplier_FailsWithSupplierInactive()
    {
        var inactive = new Supplier
        {
            Id = Guid.NewGuid(),
            Code = "SUP-002",
            Name = "Retired Vendor",
            IsActive = false,
        };
        _suppliers.Seed(inactive);

        var result = await CreateOrderHandler().HandleAsync(
            new CreatePurchaseOrderCommand(_companyId, inactive.Id, PostingDate, PostingDate,
                new[] { new CreatePurchaseOrderItem(_item.Id, 10m, 100m) }));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.SupplierInactive, result.Error!.Code);
        Assert.Empty(_purchases.Orders);
    }

    [Fact]
    public async Task Create_NoLines_FailsWithNoLines()
    {
        var result = await CreateOrderHandler().HandleAsync(
            new CreatePurchaseOrderCommand(_companyId, _supplier.Id, PostingDate, PostingDate, Items: null));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.NoLines, result.Error!.Code);
        Assert.Empty(_purchases.Orders);
    }

    [Fact]
    public async Task Create_UnknownItem_FailsWithItemNotFound()
    {
        var result = await CreateOrderHandler().HandleAsync(
            new CreatePurchaseOrderCommand(_companyId, _supplier.Id, PostingDate, PostingDate,
                new[] { new CreatePurchaseOrderItem(Guid.NewGuid(), 10m, 100m) }));

        Assert.False(result.IsSuccess);
        Assert.Equal(StockErrorCodes.ItemNotFound, result.Error!.Code);
        Assert.Empty(_purchases.Orders);
    }

    // ------------------------------------------------------------------------- submit (ordered)

    [Fact]
    public async Task Submit_DraftOrder_AdvancesToSubmitted()
    {
        var created = await CreateOrderHandler().HandleAsync(NewOrder());
        Assert.True(created.IsSuccess);

        var result = await SubmitOrderHandler().HandleAsync(
            new SubmitPurchaseOrderCommand(_companyId, created.Value!.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(PurchaseOrderStatus.Submitted, result.Value!.Status);
        Assert.Equal("PO-2026-00001", result.Value.OrderNumber);
        Assert.Equal(PurchaseOrderStatus.Submitted, _purchases.Orders[0].Status);
    }

    [Theory]
    [InlineData(PurchaseOrderStatus.Submitted)]
    [InlineData(PurchaseOrderStatus.PartiallyReceived)]
    [InlineData(PurchaseOrderStatus.Completed)]
    public async Task Submit_NonDraftOrder_FailsWithInvalidStatusTransition(PurchaseOrderStatus status)
    {
        var order = SeedOrder(status);

        var result = await SubmitOrderHandler().HandleAsync(
            new SubmitPurchaseOrderCommand(_companyId, order.Id));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(PurchaseErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Equal(status, order.Status); // untouched
    }

    [Fact]
    public async Task Submit_UnknownOrder_FailsWithPurchaseOrderNotFound()
    {
        var result = await SubmitOrderHandler().HandleAsync(
            new SubmitPurchaseOrderCommand(_companyId, Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.PurchaseOrderNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task Submit_OrderOfAnotherCompany_FailsWithPurchaseOrderNotFound()
    {
        var order = SeedOrder(PurchaseOrderStatus.Draft);

        var result = await SubmitOrderHandler().HandleAsync(
            new SubmitPurchaseOrderCommand(Guid.NewGuid(), order.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.PurchaseOrderNotFound, result.Error!.Code);
        Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
    }

    [Fact]
    public async Task Submit_ConcurrentModification_FailsWithConcurrencyConflict()
    {
        // Spec BY-06: another request saved the order between our load and our save, so the
        // RowVersion check rejects the update - the handler must surface it as a typed failure
        // (mapped to 409 by the API), never as an unhandled exception.
        var order = SeedOrder(PurchaseOrderStatus.Draft);
        _purchases.FailNextOrderUpdate = true;

        var result = await SubmitOrderHandler().HandleAsync(
            new SubmitPurchaseOrderCommand(_companyId, order.Id));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
        Assert.Contains(nameof(PurchaseOrder), result.Error!.Message);
    }

    private UpdatePurchaseOrderCommandHandler UpdateOrderHandler()
    {
        return new UpdatePurchaseOrderCommandHandler(_suppliers, _items, _purchases);
    }

    [Fact]
    public async Task Update_DraftOrder_ReplacesLinesAndCalculatesTotals()
    {
        var order = SeedOrder(PurchaseOrderStatus.Draft);

        var newItem = new Item { Id = Guid.NewGuid() };
        _items.Seed(newItem);

        var result = await UpdateOrderHandler().HandleAsync(
            new UpdatePurchaseOrderCommand(_companyId, order.Id, _supplier.Id, PostingDate, PostingDate,
                new[] { new UpdatePurchaseOrderItem(newItem.Id, 20m, 50m) }));

        Assert.True(result.IsSuccess);
        Assert.Single(order.Items);
        var line = order.Items.Single();
        Assert.Equal(newItem.Id, line.ItemId);
        Assert.Equal(20m, line.Quantity);
        Assert.Equal(50m, line.Rate);
        Assert.Equal(1000m, line.Amount);
        Assert.Equal(1000m, order.NetTotal);
        Assert.Equal(1000m, order.GrandTotal);
    }

    [Theory]
    [InlineData(PurchaseOrderStatus.Submitted)]
    [InlineData(PurchaseOrderStatus.PartiallyReceived)]
    [InlineData(PurchaseOrderStatus.Completed)]
    [InlineData(PurchaseOrderStatus.Cancelled)]
    public async Task Update_NonDraftOrder_FailsWithInvalidStatusTransition(PurchaseOrderStatus status)
    {
        var order = SeedOrder(status);
        var oldNetTotal = order.NetTotal;

        var result = await UpdateOrderHandler().HandleAsync(
            new UpdatePurchaseOrderCommand(_companyId, order.Id, _supplier.Id, PostingDate, PostingDate,
                new[] { new UpdatePurchaseOrderItem(_item.Id, 100m, 100m) }));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(PurchaseErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Equal(oldNetTotal, order.NetTotal); // untouched
    }
}


