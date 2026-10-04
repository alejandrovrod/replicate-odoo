using Erp.Application.Features.Selling.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 5.2 acceptance: the sales order workflow (Draft -&gt; Submitted) exercised through the
/// CQRS handlers - gapless SO-YYYY-NNNNN numbers, server-computed totals, the customer
/// existence/activity rules, the spec SL-02 credit gate and the invalid_status_transition /
/// credit_limit_exceeded failures the API maps to 409 - against in-memory repository doubles
/// (Constitution I.2/I.3).
/// </summary>
public sealed class SalesOrderWorkflowTests
{
    private static readonly DateOnly TransactionDate = new(2026, 3, 2);
    private static readonly DateOnly DeliveryDate = new(2026, 3, 9);

    private readonly Guid _companyId = Guid.NewGuid();

    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeCustomerRepository _customers = new();
    private readonly FakeItemRepository _items = new();
    private readonly FakeSalesOrderRepository _salesOrders = new();

    private readonly Customer _customer;
    private readonly Item _item;

    public SalesOrderWorkflowTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = Guid.NewGuid(),
            Name = "Acme Industrial",
        };

        _customer = new Customer
        {
            Id = Guid.NewGuid(),
            CompanyId = _companyId,
            CustomerCode = "CUST-001",
            CustomerName = "ACME Corp",
            IsActive = true,
        };
        _customers.Seed(_customer);

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

    private CreateSalesOrderCommandHandler CreateOrderHandler() =>
        new(_companies, _customers, _items, _salesOrders);

    private SubmitSalesOrderCommandHandler SubmitOrderHandler() =>
        new(_customers, _items, _salesOrders);

    private CreateSalesOrderCommand NewOrder(Guid? customerId = null) =>
        new(_companyId, customerId ?? _customer.Id, TransactionDate, DeliveryDate,
            new[] { new CreateSalesOrderLine(_item.Id, 10m, 100m) });

    private SalesOrder SeedOrder(SalesOrderStatus status, Guid? companyId = null, decimal grandTotal = 1000m)
    {
        var order = new SalesOrder
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId ?? _companyId,
            CustomerId = _customer.Id,
            Status = status,
            TransactionDate = TransactionDate,
            DeliveryDate = DeliveryDate,
            OrderNumber = "SO-2026-00001",
            NetTotal = grandTotal,
            TaxTotal = 0m,
            GrandTotal = grandTotal,
            CreatedAt = DateTimeOffset.UtcNow,
            Lines = new List<SalesOrderItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ItemId = _item.Id,
                    Quantity = 10m,
                    Rate = 100m,
                    Amount = grandTotal,
                },
            },
        };
        _salesOrders.SeedOrder(order);
        return order;
    }

    // ------------------------------------------------------------------------- create (draft)

    [Fact]
    public async Task Create_ValidLines_PersistsDraftWithGaplessOrderNumber()
    {
        var first = await CreateOrderHandler().HandleAsync(NewOrder());
        var second = await CreateOrderHandler().HandleAsync(NewOrder());

        Assert.True(first.IsSuccess);
        Assert.NotNull(first.Value);
        Assert.Equal("SO-2026-00001", first.Value!.OrderNumber);
        Assert.Equal(SalesOrderStatus.Draft, first.Value.Status);
        Assert.Equal("SO-2026-00002", second.Value!.OrderNumber);
        Assert.Equal(SalesOrderStatus.Draft, second.Value.Status);

        Assert.Equal(2, _salesOrders.Orders.Count);
        var saved = _salesOrders.Orders[0];
        Assert.Equal(_customer.Id, saved.CustomerId);
        var line = Assert.Single(saved.Lines);
        Assert.Equal(_item.Id, line.ItemId);
        Assert.Equal(10m, line.Quantity);
        Assert.Equal(100m, line.Rate);
        Assert.Equal(1000m, line.Amount);

        // One number+insert transaction per creation (Constitution III.4).
        Assert.Equal(2, _salesOrders.TransactionCount);
    }

    [Fact]
    public async Task Create_ComputesTotalsServerSide_NoTaxEngineYet()
    {
        // plan.md §1: NetTotal = sum(Amount), TaxTotal 0.0000 until Task 5.3, GrandTotal = NetTotal.
        var result = await CreateOrderHandler().HandleAsync(NewOrder());

        Assert.True(result.IsSuccess);
        Assert.Equal(1000m, result.Value!.NetTotal);
        Assert.Equal(0m, result.Value.TaxTotal);
        Assert.Equal(1000m, result.Value.GrandTotal);
        Assert.Equal(0m, result.Value.DeliveredPercentage);
        Assert.Equal(0m, result.Value.BilledPercentage);
    }

    [Fact]
    public async Task Create_MissingDates_FailsWithDateRequiredCodes()
    {
        var withoutTransactionDate = await CreateOrderHandler().HandleAsync(
            new CreateSalesOrderCommand(_companyId, _customer.Id, null, DeliveryDate,
                new[] { new CreateSalesOrderLine(_item.Id, 10m, 100m) }));
        Assert.False(withoutTransactionDate.IsSuccess);
        Assert.Equal(SellingErrorCodes.TransactionDateRequired, withoutTransactionDate.Error!.Code);

        var withoutDeliveryDate = await CreateOrderHandler().HandleAsync(
            new CreateSalesOrderCommand(_companyId, _customer.Id, TransactionDate, null,
                new[] { new CreateSalesOrderLine(_item.Id, 10m, 100m) }));
        Assert.False(withoutDeliveryDate.IsSuccess);
        Assert.Equal(SellingErrorCodes.DeliveryDateRequired, withoutDeliveryDate.Error!.Code);

        Assert.Empty(_salesOrders.Orders);
    }

    [Fact]
    public async Task Create_UnknownCustomer_FailsWithCustomerNotFound()
    {
        var result = await CreateOrderHandler().HandleAsync(NewOrder(Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.CustomerNotFound, result.Error!.Code);
        Assert.Empty(_salesOrders.Orders);
    }

    [Fact]
    public async Task Create_InactiveCustomer_FailsWithCustomerInactive()
    {
        var inactive = new Customer
        {
            Id = Guid.NewGuid(),
            CompanyId = _companyId,
            CustomerCode = "CUST-002",
            CustomerName = "Retired Buyer",
            IsActive = false,
        };
        _customers.Seed(inactive);

        var result = await CreateOrderHandler().HandleAsync(NewOrder(inactive.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.CustomerInactive, result.Error!.Code);
        Assert.Empty(_salesOrders.Orders);
    }

    [Fact]
    public async Task Create_NoLines_FailsWithNoLines()
    {
        var result = await CreateOrderHandler().HandleAsync(
            new CreateSalesOrderCommand(_companyId, _customer.Id, TransactionDate, DeliveryDate, Lines: null));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.NoLines, result.Error!.Code);
        Assert.Empty(_salesOrders.Orders);
    }

    [Fact]
    public async Task Create_UnknownItem_FailsWithItemNotFound()
    {
        var result = await CreateOrderHandler().HandleAsync(
            new CreateSalesOrderCommand(_companyId, _customer.Id, TransactionDate, DeliveryDate,
                new[] { new CreateSalesOrderLine(Guid.NewGuid(), 10m, 100m) }));

        Assert.False(result.IsSuccess);
        Assert.Equal(StockErrorCodes.ItemNotFound, result.Error!.Code);
        Assert.Empty(_salesOrders.Orders);
    }

    // ------------------------------------------------------------------- submit (SL-02 credit gate)

    [Fact]
    public async Task Submit_DraftOrderWithinCreditLimit_AdvancesToSubmitted()
    {
        _customer.CreditLimit = 5000m;
        _customer.OutstandingAmount = 4600m;

        var created = await CreateOrderHandler().HandleAsync(NewOrder());
        Assert.True(created.IsSuccess);

        // Exposure 4,600 + 1,000 = 5,600 breaches 5,000 -> raise the limit so this one passes AT
        // the boundary (plan.md §2: exposure exactly AT the limit is allowed).
        _customer.CreditLimit = 5600m;

        var result = await SubmitOrderHandler().HandleAsync(
            new SubmitSalesOrderCommand(_companyId, created.Value!.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(SalesOrderStatus.Submitted, result.Value!.Status);
        Assert.Equal("SO-2026-00001", result.Value.OrderNumber);
        Assert.Equal(SalesOrderStatus.Submitted, _salesOrders.Orders[0].Status);
    }

    [Fact]
    public async Task Submit_BreachedCreditLimit_FailsWithCreditLimitExceededAndOrderStaysDraft()
    {
        // spec SL-02: limit $5,000, outstanding $4,600, attempted $1,000 -> exposure $5,600.
        _customer.CreditLimit = 5000m;
        _customer.OutstandingAmount = 4600m;

        var created = await CreateOrderHandler().HandleAsync(NewOrder());
        Assert.True(created.IsSuccess);

        var result = await SubmitOrderHandler().HandleAsync(
            new SubmitSalesOrderCommand(_companyId, created.Value!.Id));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(SellingErrorCodes.CreditLimitExceeded, result.Error!.Code);
        Assert.Contains("$5000", result.Error!.Message);
        Assert.Equal(SalesOrderStatus.Draft, _salesOrders.Orders[0].Status); // untouched
    }

    [Theory]
    [InlineData(SalesOrderStatus.Submitted)]
    [InlineData(SalesOrderStatus.PartiallyDelivered)]
    [InlineData(SalesOrderStatus.Completed)]
    [InlineData(SalesOrderStatus.Cancelled)]
    public async Task Submit_NonDraftOrder_FailsWithInvalidStatusTransition(SalesOrderStatus status)
    {
        var order = SeedOrder(status);

        var result = await SubmitOrderHandler().HandleAsync(
            new SubmitSalesOrderCommand(_companyId, order.Id));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(SellingErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Equal(status, order.Status); // untouched
    }

    [Fact]
    public async Task Submit_UnknownOrder_FailsWithSalesOrderNotFound()
    {
        var result = await SubmitOrderHandler().HandleAsync(
            new SubmitSalesOrderCommand(_companyId, Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.SalesOrderNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task Submit_OrderOfAnotherCompany_FailsWithSalesOrderNotFound()
    {
        var order = SeedOrder(SalesOrderStatus.Draft);

        var result = await SubmitOrderHandler().HandleAsync(
            new SubmitSalesOrderCommand(Guid.NewGuid(), order.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.SalesOrderNotFound, result.Error!.Code);
        Assert.Equal(SalesOrderStatus.Draft, order.Status);
    }

    [Fact]
    public async Task Submit_ConcurrentModification_FailsWithConcurrencyConflict()
    {
        // Spec SL-06: the submit transition is a read-modify-write, so a RowVersion mismatch on
        // save surfaces as a typed failure (mapped to 409 by the API), never an unhandled exception.
        var order = SeedOrder(SalesOrderStatus.Draft);
        _salesOrders.FailNextOrderUpdate = true;

        var result = await SubmitOrderHandler().HandleAsync(
            new SubmitSalesOrderCommand(_companyId, order.Id));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
        Assert.Contains(nameof(SalesOrder), result.Error!.Message);
    }
}
