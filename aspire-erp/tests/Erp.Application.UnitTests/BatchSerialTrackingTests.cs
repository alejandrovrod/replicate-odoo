using Erp.Application.Features.Stock.Commands;
using Erp.Application.Services;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Batch & serial tracking Fase 3 (spec batch-and-serial-tracking, STK-TRK-G1..G4): batch
/// auto-creation on receipt with Kardex reference, expired-batch dispatch block, strict
/// serial warehouse control with state transitions, and cancellation revert - through the
/// stock posting engine and its cancel handler.
/// </summary>
public sealed class BatchSerialTrackingTests
{
    private static readonly DateOnly PostingDate = new(2026, 5, 4);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeWarehouseRepository _warehouses = new();
    private readonly FakeItemRepository _items = new();
    private readonly FakeStockRepository _stock = new();
    private readonly FakeBatchRepository _batches = new();
    private readonly FakeSerialNoRepository _serials = new();

    private readonly Warehouse _warehouse;
    private readonly Item _batchItem;
    private readonly Item _serialItem;

    public BatchSerialTrackingTests()
    {
        var stockAccount = NewAccount("1310", "Stock In Hand");
        var receivedAccount = NewAccount("2120", "Stock Received But Not Billed");
        var expenseAccount = NewAccount("5210", "Cost of Goods Sold");
        _accounts.Seed(stockAccount, receivedAccount, expenseAccount);
        _accounts.AccountsByCode = new[] { receivedAccount };
        _accounts.AccountsByCodeMap["5210"] = new[] { expenseAccount };

        _warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            WarehouseCode = "WH-01",
            WarehouseName = "Main Stores",
            AccountId = stockAccount.Id,
        };
        _warehouses.Seed(_warehouse);

        _batchItem = new Item
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ItemCode = "VAC-001",
            ItemName = "Vacuna Antigripal",
            ValuationMethod = ValuationMethod.Fifo,
            StockUomId = Guid.NewGuid(),
            HasBatchNo = true,
        };
        _serialItem = new Item
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ItemCode = "LAP-001",
            ItemName = "Laptop Ultrabook",
            ValuationMethod = ValuationMethod.Fifo,
            StockUomId = Guid.NewGuid(),
            HasSerialNo = true,
        };
        _items.Seed(_batchItem, _serialItem);

        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = _tenantId,
            Name = "Acme",
            AllowNegativeStock = false,
            StockReceivedAccountCode = "2120",
            CogsAccountCode = "5210",
        };
    }

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

    private StockPostingService Service() =>
        new(_companies, _accounts, _warehouses, _items, _stock,
            new BatchSerialService(_batches, _serials, _stock));

    private static StockPostingLine ReceiptLine(
        Guid itemId, decimal qty, string? batchNo = null, IReadOnlyList<string>? serials = null,
        DateOnly? expiry = null) =>
        new(itemId, qty, 100m, BatchNo: batchNo, ExpiryDate: expiry, SerialNumbers: serials);

    private static StockPostingLine IssueLine(
        Guid itemId, decimal qty, Guid? batchId = null, IReadOnlyList<string>? serials = null) =>
        new(itemId, qty, Rate: null, BatchId: batchId, SerialNumbers: serials);

    private StockPostingRequest Request(StockEntryType type, IReadOnlyList<StockPostingLine> lines) =>
        new(_companyId, type, PostingDate, _warehouse.Id, null, lines);

    // ------------------------------------------------------------- STK-TRK-G1 (receipt)

    [Fact]
    public async Task ReceiptBatchItem_CreatesBatchAndReferencesItInKardex()
    {
        var result = await Service().PostAsync(Request(
            StockEntryType.MaterialReceipt,
            [ReceiptLine(_batchItem.Id, 100m, "LOT-2026-001", expiry: new DateOnly(2027, 12, 31))]));

        var batch = Assert.Single(_batches.Batches);
        Assert.Equal("LOT-2026-001", batch.BatchId);
        Assert.Equal(_batchItem.Id, batch.ItemId);
        Assert.Equal(new DateOnly(2027, 12, 31), batch.ExpiryDate);
        Assert.False(batch.IsDisabled);

        // Every Kardex row of the voucher carries the batch reference...
        Assert.All(_stock.AddedLedger, row => Assert.Equal(batch.Id, row.BatchId));

        // ...so availability derives 100 units in the warehouse (G1 acceptance).
        var availability = await new BatchSerialService(_batches, _serials, _stock)
            .GetAvailabilityAsync(_batchItem.Id, _warehouse.Id, PostingDate);
        var row = Assert.Single(availability);
        Assert.Equal(batch.Id, row.BatchId);
        Assert.Equal(100m, row.Qty);
        Assert.False(row.IsExpired);

        // ...and the posting response exposes the batch per line.
        var line = Assert.Single(result.Entry.Lines);
        Assert.Equal(batch.Id, line.BatchId);
        Assert.Equal("LOT-2026-001", line.BatchNo);
    }

    [Fact]
    public async Task ReceiptBatchItem_WithoutBatch_RejectedWithBatchRequired()
    {
        var ex = await Assert.ThrowsAsync<StockValidationException>(
            () => Service().PostAsync(Request(
                StockEntryType.MaterialReceipt,
                [ReceiptLine(_batchItem.Id, 10m)])));

        Assert.Equal(StockErrorCodes.BatchRequired, ex.Code);
        Assert.Empty(_stock.AddedLedger); // zero writes
    }

    // ------------------------------------------------------------- STK-TRK-G2 (expiry)

    [Fact]
    public async Task IssueExpiredBatch_RejectedWithBatchHasExpired()
    {
        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            ItemId = _batchItem.Id,
            BatchId = "LOT-EXPIRED",
            ExpiryDate = new DateOnly(2026, 1, 1),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _batches.Seed(batch);
        _stock.SeedLedger(new StockLedgerEntry
        {
            Id = Guid.NewGuid(),
            ItemId = _batchItem.Id,
            WarehouseId = _warehouse.Id,
            BatchId = batch.Id,
            PostingDate = new DateOnly(2025, 12, 1),
            QtyChange = 10m,
            ValuationRate = 100m,
            Amount = 1000m,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        var ex = await Assert.ThrowsAsync<StockValidationException>(
            () => Service().PostAsync(Request(
                StockEntryType.MaterialIssue,
                [IssueLine(_batchItem.Id, 5m, batch.Id)])));

        Assert.Equal(StockErrorCodes.BatchHasExpired, ex.Code);
        Assert.Empty(_stock.AddedLedger); // zero writes
    }

    [Fact]
    public async Task IssueBatchBeyondBalance_RejectedWithBatchInsufficientStock()
    {
        var result = await Service().PostAsync(Request(
            StockEntryType.MaterialReceipt,
            [ReceiptLine(_batchItem.Id, 100m, "LOT-2026-001", expiry: new DateOnly(2027, 12, 31))]));
        var batchId = Assert.Single(_batches.Batches).Id;
        _ = result;

        var ex = await Assert.ThrowsAsync<StockValidationException>(
            () => Service().PostAsync(Request(
                StockEntryType.MaterialIssue,
                [IssueLine(_batchItem.Id, 101m, batchId)])));

        Assert.Equal(StockErrorCodes.BatchInsufficientStock, ex.Code);
    }

    // ------------------------------------------------------- STK-TRK-G3 (serial control)

    [Fact]
    public async Task ReceiptSerialItem_CreatesActiveSerialsInWarehouse()
    {
        await Service().PostAsync(Request(
            StockEntryType.MaterialReceipt,
            [ReceiptLine(_serialItem.Id, 2m, serials: ["SN-1001", "SN-1002"])]));

        Assert.Equal(2, _serials.Serials.Count);
        Assert.All(_serials.Serials, s =>
        {
            Assert.Equal(SerialNoStatus.Active, s.Status);
            Assert.Equal(_warehouse.Id, s.CurrentWarehouseId);
            Assert.Equal(_serialItem.Id, s.ItemId);
            Assert.NotNull(s.StockEntryItemId); // linked to the moving line
        });
    }

    [Fact]
    public async Task ReceiptSerialItem_CountMismatch_Rejected()
    {
        var ex = await Assert.ThrowsAsync<StockValidationException>(
            () => Service().PostAsync(Request(
                StockEntryType.MaterialReceipt,
                [ReceiptLine(_serialItem.Id, 2m, serials: ["SN-1001"])])));

        Assert.Equal(StockErrorCodes.SerialCountMismatch, ex.Code);
        Assert.Empty(_serials.Serials);
    }

    [Fact]
    public async Task ReceiptSerialItem_DuplicateActiveSerial_Rejected()
    {
        await Service().PostAsync(Request(
            StockEntryType.MaterialReceipt,
            [ReceiptLine(_serialItem.Id, 1m, serials: ["SN-1001"])]));

        var ex = await Assert.ThrowsAsync<StockValidationException>(
            () => Service().PostAsync(Request(
                StockEntryType.MaterialReceipt,
                [ReceiptLine(_serialItem.Id, 1m, serials: ["SN-1001"])])));

        Assert.Equal(StockErrorCodes.SerialAlreadyExists, ex.Code);
    }

    [Fact]
    public async Task IssueSerialItem_UnknownSerial_RejectedWithNotInWarehouse()
    {
        await Service().PostAsync(Request(
            StockEntryType.MaterialReceipt,
            [ReceiptLine(_serialItem.Id, 2m, serials: ["SN-1001", "SN-1002"])]));

        // STK-TRK-G3: SN-9999 is not available in the warehouse.
        var ex = await Assert.ThrowsAsync<StockValidationException>(
            () => Service().PostAsync(Request(
                StockEntryType.MaterialIssue,
                [IssueLine(_serialItem.Id, 1m, serials: ["SN-9999"])])));

        Assert.Equal(StockErrorCodes.SerialNotInWarehouse, ex.Code);
    }

    [Fact]
    public async Task IssueSerialItem_DeliversAndCancelReactivates()
    {
        var receipt = await Service().PostAsync(Request(
            StockEntryType.MaterialReceipt,
            [ReceiptLine(_serialItem.Id, 2m, serials: ["SN-1001", "SN-1002"])]));
        _ = receipt;

        var issue = await Service().PostAsync(Request(
            StockEntryType.MaterialIssue,
            [IssueLine(_serialItem.Id, 1m, serials: ["SN-1001"])]));
        // The issue response exposes the moved serials per line.
        Assert.Equal(["SN-1001"], Assert.Single(issue.Entry.Lines).SerialNumbers);

        var delivered = _serials.Serials.Single(s => s.SerialNumber == "SN-1001");
        Assert.Equal(SerialNoStatus.Delivered, delivered.Status);
        Assert.Equal(issue.Entry.VoucherNo, delivered.DeliveryDocumentNo);
        Assert.Equal(SerialNoStatus.Active, _serials.Serials.Single(s => s.SerialNumber == "SN-1002").Status);

        // STK-TRK-G4: cancelling the issue reverts the serial to Active in its warehouse.
        var cancel = new CancelStockEntryCommandHandler(_stock, _companies, _serials);
        var cancelled = await cancel.HandleAsync(
            new CancelStockEntryCommand(_companyId, issue.Entry.Id), CancellationToken.None);
        Assert.True(cancelled.IsSuccess);

        var reverted = _serials.Serials.Single(s => s.SerialNumber == "SN-1001");
        Assert.Equal(SerialNoStatus.Active, reverted.Status);
        Assert.Equal(_warehouse.Id, reverted.CurrentWarehouseId);
    }

    [Fact]
    public async Task CancelReceipt_DeletesCreatedSerials()
    {
        var receipt = await Service().PostAsync(Request(
            StockEntryType.MaterialReceipt,
            [ReceiptLine(_serialItem.Id, 1m, serials: ["SN-1001"])]));
        Assert.Single(_serials.Serials);

        var cancel = new CancelStockEntryCommandHandler(_stock, _companies, _serials);
        var cancelled = await cancel.HandleAsync(
            new CancelStockEntryCommand(_companyId, receipt.Entry.Id), CancellationToken.None);
        Assert.True(cancelled.IsSuccess);

        Assert.Empty(_serials.Serials);
    }

    // ------------------------------------------------------------------ FEFO + transfer

    [Fact]
    public async Task Availability_IsOrderedByExpiryFefo()
    {
        foreach (var (number, expiry) in new[]
                 {
                     ("LOT-LATE", new DateOnly(2028, 6, 30)),
                     ("LOT-SOON", new DateOnly(2026, 12, 31)),
                     ("LOT-NOEXP", (DateOnly?)null),
                 })
        {
            await Service().PostAsync(Request(
                StockEntryType.MaterialReceipt,
                [ReceiptLine(_batchItem.Id, 10m, number, expiry: expiry)]));
        }

        var availability = await new BatchSerialService(_batches, _serials, _stock)
            .GetAvailabilityAsync(_batchItem.Id, _warehouse.Id, PostingDate);

        Assert.Equal(["LOT-SOON", "LOT-LATE", "LOT-NOEXP"], availability.Select(a => a.BatchNo));
    }

    [Fact]
    public async Task TransferBatchAndSerials_MovesWarehouse()
    {
        var target = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            WarehouseCode = "WH-02",
            WarehouseName = "Branch",
            AccountId = _warehouse.AccountId,
        };
        _warehouses.Seed(target);

        var both = new Item
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ItemCode = "KIT-001",
            ItemName = "Tracked Kit",
            ValuationMethod = ValuationMethod.Fifo,
            StockUomId = Guid.NewGuid(),
            HasBatchNo = true,
            HasSerialNo = true,
        };
        _items.Seed(both);

        await Service().PostAsync(new StockPostingRequest(
            _companyId, StockEntryType.MaterialReceipt, PostingDate, _warehouse.Id, null,
            [new StockPostingLine(both.Id, 1m, 50m, BatchNo: "LOT-T1", SerialNumbers: ["SN-T1"])]));
        var batchId = Assert.Single(_batches.Batches).Id;

        await Service().PostAsync(new StockPostingRequest(
            _companyId, StockEntryType.MaterialTransfer, PostingDate, _warehouse.Id, target.Id,
            [new StockPostingLine(both.Id, 1m, null, BatchId: batchId, SerialNumbers: ["SN-T1"])]));

        var moved = Assert.Single(_serials.Serials);
        Assert.Equal(SerialNoStatus.Active, moved.Status);
        Assert.Equal(target.Id, moved.CurrentWarehouseId);

        var availability = await new BatchSerialService(_batches, _serials, _stock)
            .GetAvailabilityAsync(both.Id, target.Id, PostingDate);
        Assert.Equal(1m, Assert.Single(availability).Qty);
    }

    // ------------------------------------------------- purchase receipt + delivery flows

    [Fact]
    public async Task PurchaseReceiptBatchAndSerials_CreatesMastersAndKardexRefs()
    {
        var supplierId = Guid.NewGuid();
        var service = new PurchasePostingService(
            _companies, _accounts, _warehouses, _items, _stock,
            new FakePurchaseRepository(), new FakeExchangeRateRepository(), new FakeAssetsRepository(),
            tracking: new BatchSerialService(_batches, _serials, _stock));

        // The bought laptop needs both flags (coexistence, spec D1).
        _serialItem.HasBatchNo = true;

        var result = await service.PostReceiptAsync(new PurchaseReceiptPostingRequest(
            _companyId, _warehouse.Id, supplierId, null, PostingDate,
            [new PurchaseReceiptPostingLine(
                _serialItem.Id, 2m, 1500m,
                BatchNo: "LOT-PO-001",
                ExpiryDate: new DateOnly(2028, 1, 31),
                SerialNumbers: ["SN-P01", "SN-P02"])]));

        var batch = Assert.Single(_batches.Batches);
        Assert.Equal("LOT-PO-001", batch.BatchId);

        var receiptLine = Assert.Single(result.Receipt.Lines);
        Assert.Equal(batch.Id, receiptLine.BatchId);

        Assert.All(result.LedgerEntries, row => Assert.Equal(batch.Id, row.BatchId));

        Assert.Equal(2, _serials.Serials.Count);
        Assert.All(_serials.Serials, s =>
        {
            Assert.Equal(SerialNoStatus.Active, s.Status);
            Assert.Equal(_warehouse.Id, s.CurrentWarehouseId);
            Assert.Equal(batch.Id, s.BatchId);
            Assert.Equal(result.Receipt.VoucherNo, s.PurchaseDocumentNo);
        });
    }

    [Fact]
    public async Task DeliveryNoteBatchAndSerials_DeliversAndLinksDocuments()
    {
        // Stock the tracked kit first (receipt creates batch + serials end-to-end).
        var both = new Item
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ItemCode = "KIT-002",
            ItemName = "Tracked Kit 2",
            ValuationMethod = ValuationMethod.Fifo,
            StockUomId = Guid.NewGuid(),
            HasBatchNo = true,
            HasSerialNo = true,
        };
        _items.Seed(both);

        await Service().PostAsync(new StockPostingRequest(
            _companyId, StockEntryType.MaterialReceipt, PostingDate, _warehouse.Id, null,
            [new StockPostingLine(both.Id, 2m, 50m, BatchNo: "LOT-D1", SerialNumbers: ["SN-D1", "SN-D2"])]));
        var batchId = Assert.Single(_batches.Batches).Id;

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            CustomerCode = "CUST-001",
            CustomerName = "ACME Corp",
            IsActive = true,
        };

        var order = new SalesOrder
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            CustomerId = customer.Id,
            Customer = customer,
            Status = SalesOrderStatus.Submitted,
            TransactionDate = PostingDate.AddDays(-7),
            DeliveryDate = PostingDate.AddDays(7),
            OrderNumber = "SO-2026-00001",
            NetTotal = 200m,
            GrandTotal = 200m,
            CreatedAt = DateTimeOffset.UtcNow,
            Lines = new List<SalesOrderItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ItemId = both.Id,
                    Quantity = 2m,
                    DeliveredQuantity = 0m,
                    BilledQuantity = 0m,
                    Rate = 100m,
                    Amount = 200m,
                },
            },
        };

        var salesOrders = new FakeSalesOrderRepository();
        salesOrders.SeedOrder(order);

        var cogsAccount = NewAccount("5210-X", "COGS");
        _accounts.Seed(cogsAccount);
        _companies.Company!.CogsAccountCode = "5210-X";
        _accounts.AccountsByCodeMap["5210-X"] = new[] { cogsAccount };

        var delivery = new SalesPostingService(
            _companies, _accounts, _warehouses, _items, _stock,
            salesOrders, new FakeDeliveryNoteRepository(), new FakeCustomerRepository(),
            tracking: new BatchSerialService(_batches, _serials, _stock));

        var result = await delivery.PostDeliveryNoteAsync(new DeliveryNotePostingRequest(
            _companyId, order.Id, _warehouse.Id, PostingDate,
            [new DeliveryNotePostingLine(
                order.Lines.First().Id, both.Id, 1m, BatchId: batchId, SerialNumbers: ["SN-D1"])]));

        var line = Assert.Single(result.DeliveryNote.Lines);
        Assert.Equal(batchId, line.BatchId);
        Assert.All(result.LedgerEntries, row => Assert.Equal(batchId, row.BatchId));

        var shipped = _serials.Serials.Single(s => s.SerialNumber == "SN-D1");
        Assert.Equal(SerialNoStatus.Delivered, shipped.Status);
        Assert.Equal(result.DeliveryNote.VoucherNo, shipped.DeliveryDocumentNo);
        Assert.Equal(SerialNoStatus.Active, _serials.Serials.Single(s => s.SerialNumber == "SN-D2").Status);
    }
}
