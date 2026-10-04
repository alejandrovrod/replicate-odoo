using Erp.Application.Services;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 5.2b (Amendment A1) + spec SL-04: <see cref="SalesPostingService"/> orchestration - the
/// FIFO-valued shipment (Dr 5210 Cost of Goods Sold / Cr 1310 warehouse stock + -Kardex rows), the
/// gapless DN voucher, the order's delivery counters/status and the non-overdelivery guard -
/// against in-memory repository doubles, so no EF Core and no database are involved
/// (Constitution I.2/I.3).
/// </summary>
public sealed class SalesPostingServiceTests
{
    private static readonly DateOnly PostingDate = new(2026, 3, 2);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeWarehouseRepository _warehouses = new();
    private readonly FakeItemRepository _items = new();
    private readonly FakeStockRepository _stock = new();
    private readonly FakeSalesOrderRepository _salesOrders = new();
    private readonly FakeDeliveryNoteRepository _deliveryNotes = new();
    private readonly FakeCustomerRepository _customers = new();

    private readonly Account _stockAccount; // 1310 - Stock In Hand (warehouse asset)
    private readonly Account _cogsAccount;  // 5210 - Cost of Goods Sold
    private readonly Warehouse _warehouse;
    private readonly Item _item;
    private readonly Customer _customer;

    public SalesPostingServiceTests()
    {
        _stockAccount = NewAccount("1310", "Stock In Hand");
        _cogsAccount = NewAccount("5210", "Cost of Goods Sold");
        _accounts.Seed(_stockAccount, _cogsAccount);
        _accounts.AccountsByCodeMap["5210"] = new[] { _cogsAccount };

        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = _tenantId,
            Name = "Acme Industrial",
            AllowNegativeStock = false,
            CogsAccountCode = "5210",
        };

        _warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            WarehouseCode = "WH-01",
            WarehouseName = "Main Stores",
            AccountId = _stockAccount.Id,
        };
        _warehouses.Seed(_warehouse);

        _item = new Item
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ItemCode = "IT-001",
            ItemName = "Steel Bracket",
            ValuationMethod = ValuationMethod.Fifo,
            StockUomId = Guid.NewGuid(),
        };
        _items.Seed(_item);

        _customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            CustomerCode = "CUST-001",
            CustomerName = "ACME Corp",
            IsActive = true,
        };
        _customers.Seed(_customer);
    }

    private SalesPostingService CreateService() =>
        new(_companies, _accounts, _warehouses, _items, _stock, _salesOrders, _deliveryNotes, _customers);

    private Account NewAccount(string code, string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            AccountCode = code,
            AccountName = name,
            IsActive = true,
            IsGroup = false,
        };

    private SalesOrder SeedOrder(SalesOrderStatus status, decimal quantity = 10m)
    {
        var order = new SalesOrder
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            CustomerId = _customer.Id,
            Customer = _customer,
            Status = status,
            TransactionDate = PostingDate.AddDays(-7),
            DeliveryDate = PostingDate.AddDays(7),
            OrderNumber = "SO-2026-00001",
            NetTotal = quantity * 100m,
            TaxTotal = 0m,
            GrandTotal = quantity * 100m,
            CreatedAt = DateTimeOffset.UtcNow,
            Lines = new List<SalesOrderItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ItemId = _item.Id,
                    Quantity = quantity,
                    DeliveredQuantity = 0m,
                    BilledQuantity = 0m,
                    Rate = 100m,
                    Amount = quantity * 100m,
                },
            },
        };
        _salesOrders.SeedOrder(order);
        return order;
    }

    private void SeedFifoLayers(decimal qty = 10m, decimal rate = 100m)
    {
        // One MaterialReceipt layer well before the delivery: qty @ rate.
        _stock.SeedLedger(LedgerRow(+qty, rate, PostingDate.AddDays(-2)));
    }

    private StockLedgerEntry LedgerRow(decimal qtyChange, decimal rate, DateOnly postingDate) =>
        new()
        {
            Id = Guid.NewGuid(),
            ItemId = _item.Id,
            WarehouseId = _warehouse.Id,
            PostingDate = postingDate,
            QtyChange = qtyChange,
            ValuationRate = rate,
            Amount = qtyChange * rate,
            CreatedAt = new DateTimeOffset(postingDate, TimeOnly.MinValue, TimeSpan.Zero),
        };

    private DeliveryNotePostingRequest Request(
        SalesOrder order,
        decimal qty,
        Guid? salesOrderItemId = null,
        Guid? itemId = null) =>
        new(
            _companyId,
            order.Id,
            _warehouse.Id,
            PostingDate,
            new[]
            {
                new DeliveryNotePostingLine(
                    salesOrderItemId ?? order.Lines.First().Id,
                    itemId ?? _item.Id,
                    qty),
            });

    // ------------------------------------------------------------------------ happy path (SL-01)

    [Fact]
    public async Task PostDeliveryNoteAsync_SingleLine_WritesBalancedCogsAndKardexRows()
    {
        SeedFifoLayers(qty: 10m, rate: 100m);
        var order = SeedOrder(SalesOrderStatus.Submitted);

        var result = await CreateService().PostDeliveryNoteAsync(Request(order, qty: 10m));

        // Task 5.2b: gapless DN-YYYY-NNNNN voucher, first delivery of the year.
        Assert.Equal("DN-2026-00001", result.DeliveryNote.VoucherNo);

        // Balanced COGS pair: Dr 5210 Cost of Goods Sold $1,000 / Cr 1310 Stock In Hand $1,000.
        Assert.Equal(1000m, result.TotalDebit);
        Assert.Equal(1000m, result.TotalCredit);
        Assert.Equal(2, result.GlEntries.Count);
        var debit = result.GlEntries.Single(g => g.AccountCode == "5210");
        Assert.Equal(1000m, debit.Debit);
        Assert.Equal(0m, debit.Credit);
        var credit = result.GlEntries.Single(g => g.AccountCode == "1310");
        Assert.Equal(0m, credit.Debit);
        Assert.Equal(1000m, credit.Credit);
        Assert.All(result.GlEntries, g =>
        {
            Assert.Equal("DeliveryNote", g.VoucherType);
            Assert.Equal("DN-2026-00001", g.VoucherNo);
        });

        // The customer counterparty rides on the GL lines (plan.md §2).
        Assert.All(_stock.AddedGlEntries, g =>
        {
            Assert.Equal("Customer", g.PartyType);
            Assert.Equal(_customer.Id, g.PartyId);
            Assert.Equal("DN-2026-00001", g.VoucherNo);
        });

        // Kardex: -10 units valued at FIFO cost, stamped with the voucher provenance and NO
        // StockEntryId (the delivery note is not a stock voucher).
        var ledger = Assert.Single(result.LedgerEntries);
        Assert.Equal(-10m, ledger.QtyChange);
        Assert.Equal(100m, ledger.ValuationRate);
        Assert.Equal(-1000m, ledger.Amount);
        var ledgerRow = _stock.AddedLedger.Single();
        Assert.Equal("DeliveryNote", ledgerRow.VoucherType);
        Assert.Equal("DN-2026-00001", ledgerRow.VoucherNo);
        Assert.Null(ledgerRow.StockEntryId);

        // ONE transaction wraps guard + FIFO + Kardex + GL + number + save + order update.
        Assert.Equal(1, _deliveryNotes.TransactionCount);

        var note = Assert.Single(_deliveryNotes.Notes);
        Assert.Equal(_warehouse.Id, note.WarehouseId);
        Assert.Equal(order.Id, note.SalesOrderId);
        var noteLine = Assert.Single(note.Lines);
        Assert.Equal(order.Lines.First().Id, noteLine.SalesOrderItemId);
        Assert.Equal(_item.Id, noteLine.ItemId);
        Assert.Equal(10m, noteLine.Qty);
    }

    [Fact]
    public async Task PostDeliveryNoteAsync_SecondDelivery_GetsNextGaplessVoucher()
    {
        SeedFifoLayers(qty: 20m, rate: 100m);
        var order = SeedOrder(SalesOrderStatus.Submitted);

        var service = CreateService();
        await service.PostDeliveryNoteAsync(Request(order, qty: 5m));
        await service.PostDeliveryNoteAsync(Request(order, qty: 5m));

        Assert.Equal("DN-2026-00001", _deliveryNotes.Notes[0].VoucherNo);
        Assert.Equal("DN-2026-00002", _deliveryNotes.Notes[1].VoucherNo);
    }

    [Fact]
    public async Task PostDeliveryNoteAsync_PartialDelivery_AdvancesOrderToPartiallyDelivered()
    {
        SeedFifoLayers(qty: 10m, rate: 100m);
        var order = SeedOrder(SalesOrderStatus.Submitted);

        var result = await CreateService().PostDeliveryNoteAsync(Request(order, qty: 4m));

        Assert.Equal(SalesOrderStatus.PartiallyDelivered, result.SalesOrder.Status);
        Assert.Equal(SalesOrderStatus.PartiallyDelivered, order.Status);
        Assert.Equal(4m, order.Lines.First().DeliveredQuantity);
        Assert.Equal(40m, order.DeliveredPercentage); // quantity-weighted: 4 / 10 * 100
        Assert.Equal(0m, order.BilledPercentage);     // Task 5.3's column - never touched here
        Assert.Equal(0m, result.SalesOrder.BilledPercentage);
    }

    [Fact]
    public async Task PostDeliveryNoteAsync_FullDelivery_CompletesOrder()
    {
        SeedFifoLayers(qty: 10m, rate: 100m);
        var order = SeedOrder(SalesOrderStatus.Submitted);

        var result = await CreateService().PostDeliveryNoteAsync(Request(order, qty: 10m));

        Assert.Equal(SalesOrderStatus.Completed, result.SalesOrder.Status);
        Assert.Equal(10m, order.Lines.First().DeliveredQuantity);
        Assert.Equal(100m, order.DeliveredPercentage);
    }

    // ------------------------------------------------------------------- rejections (zero writes)

    [Theory]
    [InlineData(SalesOrderStatus.Draft)]
    [InlineData(SalesOrderStatus.Completed)]
    [InlineData(SalesOrderStatus.Cancelled)]
    public async Task PostDeliveryNoteAsync_OrderNotDeliverable_FailsWithSalesOrderNotDeliverable(
        SalesOrderStatus status)
    {
        SeedFifoLayers();
        var order = SeedOrder(status);

        var ex = await Assert.ThrowsAsync<SalesValidationException>(
            () => CreateService().PostDeliveryNoteAsync(Request(order, qty: 10m)));

        Assert.Equal(SellingErrorCodes.SalesOrderNotDeliverable, ex.Code);

        // Nothing was written: no note, no Kardex, no GL, order untouched.
        Assert.Empty(_deliveryNotes.Notes);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
        Assert.Equal(status, order.Status);
        Assert.Equal(0m, order.Lines.First().DeliveredQuantity);
    }

    [Fact]
    public async Task PostDeliveryNoteAsync_Overdelivery_FailsWithOverdeliveryNotAllowedBeforeAnyWrite()
    {
        // spec SL-04: remaining = 10 - 0 = 10; the note asks for 11.
        SeedFifoLayers(qty: 50m, rate: 100m);
        var order = SeedOrder(SalesOrderStatus.Submitted);

        var ex = await Assert.ThrowsAsync<OverdeliveryNotAllowedException>(
            () => CreateService().PostDeliveryNoteAsync(Request(order, qty: 11m)));

        Assert.Equal(SellingErrorCodes.OverdeliveryNotAllowed, ex.Code);
        Assert.Equal(10m, ex.Remaining);
        Assert.Equal(11m, ex.Requested);
        Assert.Equal("SO-2026-00001", ex.OrderNumber);

        // ALL SL-04 checks run BEFORE any FIFO layer is consumed: zero rows, order untouched.
        Assert.Empty(_deliveryNotes.Notes);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
        Assert.Equal(0m, order.Lines.First().DeliveredQuantity);
        Assert.Equal(SalesOrderStatus.Submitted, order.Status);
    }

    [Fact]
    public async Task PostDeliveryNoteAsync_OverdeliveryOnPartiallyDeliveredLine_UsesRemainingQuantity()
    {
        // The guard is against Quantity - DeliveredQuantity, not against the ordered quantity.
        SeedFifoLayers(qty: 50m, rate: 100m);
        var order = SeedOrder(SalesOrderStatus.PartiallyDelivered);
        order.Lines.First().DeliveredQuantity = 6m;

        var ex = await Assert.ThrowsAsync<OverdeliveryNotAllowedException>(
            () => CreateService().PostDeliveryNoteAsync(Request(order, qty: 5m)));

        Assert.Equal(SellingErrorCodes.OverdeliveryNotAllowed, ex.Code);
        Assert.Equal(4m, ex.Remaining);
        Assert.Empty(_deliveryNotes.Notes);
    }

    [Fact]
    public async Task PostDeliveryNoteAsync_ForeignOrderLine_FailsWithSalesOrderLineMismatch()
    {
        SeedFifoLayers();
        var order = SeedOrder(SalesOrderStatus.Submitted);

        var ex = await Assert.ThrowsAsync<SalesValidationException>(
            () => CreateService().PostDeliveryNoteAsync(Request(order, qty: 10m, salesOrderItemId: Guid.NewGuid())));

        Assert.Equal(SellingErrorCodes.SalesOrderLineMismatch, ex.Code);
        Assert.Empty(_deliveryNotes.Notes);
        Assert.Empty(_stock.AddedLedger);
    }

    [Fact]
    public async Task PostDeliveryNoteAsync_LineItemMismatch_FailsWithSalesOrderLineMismatch()
    {
        SeedFifoLayers();
        var order = SeedOrder(SalesOrderStatus.Submitted);
        var otherItem = new Item
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ItemCode = "IT-002",
            ItemName = "Copper Pipe",
            ValuationMethod = ValuationMethod.Fifo,
            StockUomId = Guid.NewGuid(),
        };
        _items.Seed(otherItem);

        var ex = await Assert.ThrowsAsync<SalesValidationException>(
            () => CreateService().PostDeliveryNoteAsync(Request(order, qty: 10m, itemId: otherItem.Id)));

        Assert.Equal(SellingErrorCodes.SalesOrderLineMismatch, ex.Code);
        Assert.Empty(_deliveryNotes.Notes);
    }

    [Fact]
    public async Task PostDeliveryNoteAsync_WithoutStockLayers_FailsWithInsufficientStock()
    {
        // No Kardex rows at all: FIFO runs dry and AllowNegativeStock = false.
        var order = SeedOrder(SalesOrderStatus.Submitted);

        var ex = await Assert.ThrowsAsync<InsufficientStockException>(
            () => CreateService().PostDeliveryNoteAsync(Request(order, qty: 10m)));

        Assert.Equal(StockErrorCodes.InsufficientStock, ex.Code);
        Assert.Empty(_deliveryNotes.Notes);
        Assert.Empty(_stock.AddedLedger);
        Assert.Equal(0m, order.Lines.First().DeliveredQuantity);
    }

    [Fact]
    public async Task PostDeliveryNoteAsync_NonFifoItem_ThrowsNotSupportedBeforeAnyWrite()
    {
        _item.ValuationMethod = ValuationMethod.MovingAverage;
        SeedFifoLayers();
        var order = SeedOrder(SalesOrderStatus.Submitted);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => CreateService().PostDeliveryNoteAsync(Request(order, qty: 10m)));

        Assert.Empty(_deliveryNotes.Notes);
        Assert.Empty(_stock.AddedLedger);
    }

    // ------------------------------------------------------- configuration & concurrency failures

    [Fact]
    public async Task PostDeliveryNoteAsync_MissingCogsAccountCode_FailsWithConfigurationException()
    {
        SeedFifoLayers();
        var order = SeedOrder(SalesOrderStatus.Submitted);
        _companies.Company!.CogsAccountCode = null;

        await Assert.ThrowsAsync<StockPostingConfigurationException>(
            () => CreateService().PostDeliveryNoteAsync(Request(order, qty: 10m)));

        Assert.Empty(_deliveryNotes.Notes);
        Assert.Empty(_stock.AddedLedger);
    }

    [Fact]
    public async Task PostDeliveryNoteAsync_WarehouseWithoutStockAccount_FailsWithConfigurationException()
    {
        SeedFifoLayers();
        var order = SeedOrder(SalesOrderStatus.Submitted);
        _warehouse.AccountId = Guid.Empty;

        await Assert.ThrowsAsync<StockPostingConfigurationException>(
            () => CreateService().PostDeliveryNoteAsync(Request(order, qty: 10m)));

        Assert.Empty(_deliveryNotes.Notes);
    }

    [Fact]
    public async Task PostDeliveryNoteAsync_ConcurrentOrderUpdate_ThrowsConcurrencyConflict()
    {
        // Spec SL-06: the order update is a read-modify-write, so a RowVersion mismatch on save
        // surfaces as a typed domain exception that bubbles to the CQRS handler (409). The
        // service must NOT swallow it.
        SeedFifoLayers();
        var order = SeedOrder(SalesOrderStatus.Submitted);
        _salesOrders.FailNextOrderUpdate = true;

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => CreateService().PostDeliveryNoteAsync(Request(order, qty: 10m)));

        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, ex.Code);
        Assert.Equal(order.Id, ex.EntityId);
    }

    // ------------------------------------------------ fiscal period lock (task 2.2 / AC-04)

    [Fact]
    public async Task PostDeliveryNoteAsync_BackDatedAgainstFrozenCompany_ThrowsFiscalPeriodLockAndWritesNothing()
    {
        // spec AC-04: freeze 2025-12-31, the delivery attempts to post on 2025-12-15.
        SeedFifoLayers();
        var order = SeedOrder(SalesOrderStatus.Submitted);
        _companies.Company!.FrozenAccountsDate = new DateOnly(2025, 12, 31);

        var request = new DeliveryNotePostingRequest(
            _companyId, order.Id, _warehouse.Id, new DateOnly(2025, 12, 15),
            new[] { new DeliveryNotePostingLine(order.Lines.First().Id, _item.Id, 10m) });

        var ex = await Assert.ThrowsAsync<FiscalPeriodLockedException>(
            () => CreateService().PostDeliveryNoteAsync(request));

        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, ex.Code);

        // AC-04: "no data is modified" - the lock check runs before the FIRST line is built.
        Assert.Empty(_deliveryNotes.Notes);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
        Assert.Equal(SalesOrderStatus.Submitted, order.Status);
    }

    // ---------------------------------------------- double-entry invariant (task 2.1 / AC-01)

    [Fact]
    public async Task DeliveryPostings_SatisfyDoubleEntryZeroSumInvariant()
    {
        // Task 2.1 acceptance AT THE SERVICE LEVEL: every posted delivery must obey spec AC-01
        // |sum D - sum C| <= 0.0001.
        SeedFifoLayers(qty: 30m, rate: 100m);
        var order = SeedOrder(SalesOrderStatus.Submitted);

        var service = CreateService();
        var partial = await service.PostDeliveryNoteAsync(Request(order, qty: 4m));
        var rest = await service.PostDeliveryNoteAsync(Request(order, qty: 6m));

        Assert.True(
            Math.Abs(partial.TotalDebit - partial.TotalCredit) <= 0.0001m,
            $"Delivery {partial.DeliveryNote.VoucherNo} is out of balance: "
            + $"D={partial.TotalDebit:0.0000}, C={partial.TotalCredit:0.0000}.");
        Assert.True(
            Math.Abs(rest.TotalDebit - rest.TotalCredit) <= 0.0001m,
            $"Delivery {rest.DeliveryNote.VoucherNo} is out of balance: "
            + $"D={rest.TotalDebit:0.0000}, C={rest.TotalCredit:0.0000}.");
        Assert.Equal(SalesOrderStatus.Completed, order.Status);
    }
}
