using Erp.Application.Features.Manufacturing.Commands;
using Erp.Application.Services;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 9.6 / spec MF-05: the work-order cancellation exercised through the CQRS handler
/// against in-memory doubles - the pure status transition when nothing was issued, the
/// compensating WIP -&gt; Stores transfer (swapped GL mirror, WIP back to zero) when the MF-02
/// voucher exists, the terminal-state rejections, and the guarantee that every rejection
/// writes ZERO rows.
/// </summary>
public sealed class CancelWorkOrderTests
{
    private static readonly DateOnly LayerDate = new(2026, 4, 1);
    private static readonly DateOnly PostingDate = new(2026, 4, 3);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _itemAId = Guid.NewGuid();
    private readonly Guid _itemBId = Guid.NewGuid();
    private readonly Guid _fgItemId = Guid.NewGuid();
    private readonly Guid _storesId = Guid.NewGuid();
    private readonly Guid _wipId = Guid.NewGuid();
    private readonly Guid _targetId = Guid.NewGuid();
    private readonly Guid _bomId = Guid.NewGuid();

    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeWarehouseRepository _warehouses = new();
    private readonly FakeItemRepository _items = new();
    private readonly FakeStockRepository _stock = new();
    private readonly FakeManufacturingRepository _manufacturing = new();

    private readonly Account _storesAccount;
    private readonly Account _wipAccount;

    public CancelWorkOrderTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = _tenantId,
            Name = "Acme Industrial",
            AllowNegativeStock = false,
            StockReceivedAccountCode = "2120",
            CogsAccountCode = "5210",
        };

        _storesAccount = NewAccount("1310", "Stock In Hand");
        _wipAccount = NewAccount("1320", "Work In Progress Stock");
        _accounts.Seed(_storesAccount, _wipAccount);

        _warehouses.Seed(
            NewWarehouse(_storesId, "STORES", _storesAccount.Id),
            NewWarehouse(_wipId, "WIP", _wipAccount.Id),
            NewWarehouse(_targetId, "FG", _storesAccount.Id));

        _items.Seed(NewItem(_itemAId, "COMP-A"), NewItem(_itemBId, "COMP-B"), NewItem(_fgItemId, "FG-001"));

        _manufacturing.SeedBom(new BillOfMaterials
        {
            Id = _bomId,
            TenantId = _tenantId,
            CompanyId = _companyId,
            BomNumber = "BOM-001",
            ItemId = _fgItemId,
            Quantity = 1m,
            IsActive = true,
            IsDefault = true,
            Items =
            {
                new BomItem { Id = Guid.NewGuid(), ItemId = _itemAId, Quantity = 2m, ValuationRate = 15m, Amount = 30m },
                new BomItem { Id = Guid.NewGuid(), ItemId = _itemBId, Quantity = 1m, ValuationRate = 20m, Amount = 20m },
            },
        });
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

    private Warehouse NewWarehouse(Guid id, string code, Guid accountId) =>
        new()
        {
            Id = id,
            TenantId = _tenantId,
            CompanyId = _companyId,
            WarehouseCode = code,
            WarehouseName = code,
            AccountId = accountId,
        };

    private Item NewItem(Guid id, string code) =>
        new()
        {
            Id = id,
            TenantId = _tenantId,
            CompanyId = _companyId,
            ItemCode = code,
            ItemName = code,
            ValuationMethod = ValuationMethod.Fifo,
        };

    private StockLedgerEntry LedgerRow(Guid itemId, Guid warehouseId, decimal qty, decimal rate) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ItemId = itemId,
            WarehouseId = warehouseId,
            VoucherType = "StockEntry",
            VoucherNo = "MT-2026-00001",
            PostingDate = LayerDate,
            QtyChange = qty,
            ValuationRate = rate,
            Amount = qty * rate,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
        };

    private CancelWorkOrderCommandHandler Handler() =>
        new(_manufacturing, new StockPostingService(_companies, _accounts, _warehouses, _items, _stock));

    private WorkOrder SeedOrder(
        WorkOrderStatus status = WorkOrderStatus.Submitted,
        Guid? transferStockEntryId = null,
        decimal producedQuantity = 0m)
    {
        var order = new WorkOrder
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            OrderNumber = "WO-2026-00001",
            ProductionItemId = _fgItemId,
            BomId = _bomId,
            QuantityToProduce = 10m,
            ProducedQuantity = producedQuantity,
            Status = status,
            SourceWarehouseId = _storesId,
            WipWarehouseId = _wipId,
            TargetWarehouseId = _targetId,
            TransferStockEntryId = transferStockEntryId,
            PlannedStartDate = new DateOnly(2026, 4, 1),
            PlannedEndDate = new DateOnly(2026, 4, 10),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _manufacturing.SeedWorkOrder(order);
        return order;
    }

    // --------------------------------------------------------------- pure status transition

    [Fact]
    public async Task Cancel_SubmittedWithoutTransfer_CancelsWithoutWritingRows()
    {
        var order = SeedOrder(WorkOrderStatus.Submitted);

        var result = await Handler().HandleAsync(
            new CancelWorkOrderCommand(_companyId, order.Id, PostingDate), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkOrderStatus.Cancelled, result.Value!.Status);
        Assert.Equal(WorkOrderStatus.Cancelled, order.Status);
        Assert.Equal(PostingDate, order.ActualEndDate);

        // Pure transition: no voucher, no Kardex, no GL, no transaction opened.
        Assert.Empty(_stock.StockEntries);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
        Assert.Equal(0, _stock.TransactionCount);
    }

    // ------------------------------------------------------------------ MF-05 compensating path

    /// <summary>
    /// MF-05: the order holds the MF-02 transfer link and WIP carries 20xA @ $15 + 10xB @ $20.
    /// Cancel posts a NEW compensating MT voucher WIP -&gt; Stores, WIP nets to zero at the
    /// original rates, and the GL mirrors (Dr 1310 $500 / Cr 1320 $500).
    /// </summary>
    [Fact]
    public async Task Cancel_InProcessWithTransfer_PostsCompensatingVoucherAndCancels()
    {
        var order = SeedOrder(WorkOrderStatus.InProcess, transferStockEntryId: Guid.NewGuid());
        _stock.SeedLedger(
            LedgerRow(_itemAId, _wipId, +20m, 15m),
            LedgerRow(_itemBId, _wipId, +10m, 20m));

        var result = await Handler().HandleAsync(
            new CancelWorkOrderCommand(_companyId, order.Id, PostingDate), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkOrderStatus.Cancelled, order.Status);
        Assert.Equal(PostingDate, order.ActualEndDate);

        // One NEW compensating voucher, WIP -&gt; Stores (the MF-02 direction reversed).
        var voucher = Assert.Single(_stock.StockEntries);
        Assert.Equal(StockEntryType.MaterialTransfer, voucher.EntryType);
        Assert.Equal(_wipId, voucher.WarehouseId);
        Assert.Equal(_storesId, voucher.TargetWarehouseId);

        // WIP relieved at the ORIGINAL FIFO rates: -20xA @ $15, -10xB @ $20.
        var wipRows = _stock.AddedLedger.Where(e => e.WarehouseId == _wipId).ToList();
        Assert.Equal(-30m, wipRows.Sum(e => e.QtyChange));
        Assert.Equal(-500m, wipRows.Sum(e => e.Amount));

        // Stores receives the components back: +30 units, +$500.
        Assert.Equal(30m, _stock.AddedLedger.Where(e => e.WarehouseId == _storesId).Sum(e => e.QtyChange));

        // Swapped GL mirror: Dr 1310 $500 / Cr 1320 $500, balanced by construction.
        Assert.Equal(500m, _stock.AddedGlEntries.Where(g => g.AccountId == _storesAccount.Id).Sum(g => g.Debit));
        Assert.Equal(500m, _stock.AddedGlEntries.Where(g => g.AccountId == _wipAccount.Id).Sum(g => g.Credit));
        Assert.Equal(
            _stock.AddedGlEntries.Sum(g => g.Debit),
            _stock.AddedGlEntries.Sum(g => g.Credit));
    }

    // ----------------------------------------------------------------------- rejection paths

    [Fact]
    public async Task Cancel_DraftOrder_FailsWithInvalidStatusTransitionAndWritesNothing()
    {
        var order = SeedOrder(WorkOrderStatus.Draft);

        var result = await Handler().HandleAsync(
            new CancelWorkOrderCommand(_companyId, order.Id, PostingDate), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.Draft, order.Status);
        Assert.Empty(_stock.StockEntries);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Cancel_CompletedOrder_FailsWithInvalidStatusTransitionAndWritesNothing()
    {
        // Manufacture already completed: FIFO layers must never be unpicked.
        var order = SeedOrder(WorkOrderStatus.Completed, transferStockEntryId: Guid.NewGuid(), producedQuantity: 10m);

        var result = await Handler().HandleAsync(
            new CancelWorkOrderCommand(_companyId, order.Id, PostingDate), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.Completed, order.Status);
        Assert.Empty(_stock.StockEntries);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelledOrder_FailsWithInvalidStatusTransition()
    {
        var order = SeedOrder(WorkOrderStatus.Cancelled);

        var result = await Handler().HandleAsync(
            new CancelWorkOrderCommand(_companyId, order.Id, PostingDate), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public async Task Cancel_UnknownOrder_FailsWithWorkOrderNotFoundAndWritesNothing()
    {
        var result = await Handler().HandleAsync(
            new CancelWorkOrderCommand(_companyId, Guid.NewGuid(), PostingDate), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.WorkOrderNotFound, result.Error!.Code);
        Assert.Empty(_stock.StockEntries);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Cancel_WipDrained_FailsWithInsufficientStockAndLeavesInProcess()
    {
        var order = SeedOrder(WorkOrderStatus.InProcess, transferStockEntryId: Guid.NewGuid());
        _stock.SeedLedger(LedgerRow(_itemAId, _wipId, +2m, 15m)); // only 2xA left, 20 required

        var result = await Handler().HandleAsync(
            new CancelWorkOrderCommand(_companyId, order.Id, PostingDate), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(StockErrorCodes.InsufficientStock, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.InProcess, order.Status);
    }

    [Fact]
    public async Task Cancel_FrozenPeriod_FailsWithFiscalPeriodLockedAndWritesNothing()
    {
        _companies.Company!.FrozenAccountsDate = new DateOnly(2026, 12, 31);
        var order = SeedOrder(WorkOrderStatus.InProcess, transferStockEntryId: Guid.NewGuid());
        _stock.SeedLedger(
            LedgerRow(_itemAId, _wipId, +20m, 15m),
            LedgerRow(_itemBId, _wipId, +10m, 20m));

        var result = await Handler().HandleAsync(
            new CancelWorkOrderCommand(_companyId, order.Id, PostingDate), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.InProcess, order.Status);
        Assert.Null(order.ActualEndDate);
        Assert.Empty(_stock.StockEntries);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Cancel_ConcurrencyConflict_FailsWithConcurrencyConflict()
    {
        var order = SeedOrder(WorkOrderStatus.Submitted);
        _manufacturing.FailNextWorkOrderUpdate = true;

        var result = await Handler().HandleAsync(
            new CancelWorkOrderCommand(_companyId, order.Id, PostingDate), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("concurrency_conflict", result.Error!.Code);
    }
}
