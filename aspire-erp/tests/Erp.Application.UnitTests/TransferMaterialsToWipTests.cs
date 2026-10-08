using Erp.Application.Features.Manufacturing.Commands;
using Erp.Application.Services;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 9.4 / spec MF-02: the Stores -&gt; WIP material transfer exercised through the CQRS handler
/// against in-memory doubles - the BOM x quantity lines, the EXACT Dr 1320 / Cr 1310 pair at FIFO
/// value ($500 for 20xA @ $15 + 10xB @ $20), the Submitted -&gt; InProcess transition, and the
/// guarantee that every rejection leaves the order untouched and writes ZERO stock rows.
/// </summary>
public sealed class TransferMaterialsToWipTests
{
    private static readonly DateOnly LayerDate = new(2026, 4, 1);
    private static readonly DateOnly PostingDate = new(2026, 4, 2);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _itemAId = Guid.NewGuid();
    private readonly Guid _itemBId = Guid.NewGuid();
    private readonly Guid _fgItemId = Guid.NewGuid();
    private readonly Guid _storesId = Guid.NewGuid();
    private readonly Guid _wipId = Guid.NewGuid();
    private readonly Guid _targetId = Guid.NewGuid();

    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeWarehouseRepository _warehouses = new();
    private readonly FakeItemRepository _items = new();
    private readonly FakeStockRepository _stock = new();
    private readonly FakeManufacturingRepository _manufacturing = new();

    private readonly Account _storesAccount;
    private readonly Account _wipAccount;
    private readonly Guid _bomId = Guid.NewGuid();

    public TransferMaterialsToWipTests()
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

        // Stores on hand: 20xA @ $15 + 10xB @ $20 = $500 (the MF-02 pool).
        _stock.SeedLedger(
            LedgerRow(_itemAId, _storesId, +20m, 15m),
            LedgerRow(_itemBId, _storesId, +10m, 20m));
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
            VoucherNo = "MR-2026-00001",
            PostingDate = LayerDate,
            QtyChange = qty,
            ValuationRate = rate,
            Amount = qty * rate,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
        };

    private TransferMaterialsToWipCommandHandler Handler() =>
        new(_manufacturing, new StockPostingService(_companies, _accounts, _warehouses, _items, _stock));

    private WorkOrder SeedOrder(WorkOrderStatus status = WorkOrderStatus.Submitted, Guid? companyId = null)
    {
        var order = new WorkOrder
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = companyId ?? _companyId,
            OrderNumber = "WO-2026-00001",
            ProductionItemId = _fgItemId,
            BomId = _bomId,
            QuantityToProduce = 10m,
            Status = status,
            SourceWarehouseId = _storesId,
            WipWarehouseId = _wipId,
            TargetWarehouseId = _targetId,
            PlannedStartDate = new DateOnly(2026, 4, 1),
            PlannedEndDate = new DateOnly(2026, 4, 10),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _manufacturing.SeedWorkOrder(order);
        return order;
    }

    // ------------------------------------------------------------------ happy path (spec MF-02)

    /// <summary>
    /// MF-02 verbatim: 10 assemblies x (2xA + 1xB) move Stores -&gt; WIP, and the voucher debits
    /// 1320 $500.00 / credits 1310 $500.00 at FIFO value while the order starts production.
    /// </summary>
    [Fact]
    public async Task Transfer_SubmittedOrder_PostsExactWipPairAndStartsProduction()
    {
        var order = SeedOrder();

        var result = await Handler().HandleAsync(
            new TransferMaterialsToWipCommand(_companyId, order.Id, PostingDate), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var posting = result.Value!;

        // Lines = BOM x quantity: 20xA, 10xB (order irrelevant - assert the multiset).
        Assert.Equal(2, posting.Entry.Lines.Count);
        Assert.Equal(
            new[] { 10m, 20m },
            posting.Entry.Lines.Select(l => l.Qty).OrderBy(q => q).ToArray());
        Assert.Equal(StockEntryType.MaterialTransfer, posting.Entry.EntryType);

        // The EXACT MF-02 pair: Dr 1320 $500 / Cr 1310 $500 (two Dr lines + two Cr lines netted).
        Assert.Equal(500m, posting.GlEntries.Where(g => g.AccountCode == "1320").Sum(g => g.Debit));
        Assert.Equal(0m, posting.GlEntries.Where(g => g.AccountCode == "1320").Sum(g => g.Credit));
        Assert.Equal(500m, posting.GlEntries.Where(g => g.AccountCode == "1310").Sum(g => g.Credit));
        Assert.Equal(0m, posting.GlEntries.Where(g => g.AccountCode == "1310").Sum(g => g.Debit));
        Assert.Equal(posting.TotalDebit, posting.TotalCredit);

        // WIP holds the components now: +20xA / +10xB under the transfer voucher.
        var wipRows = _stock.AddedLedger.Where(e => e.WarehouseId == _wipId).ToList();
        Assert.Equal(30m, wipRows.Sum(e => e.QtyChange));

        // The order started production with its actual start stamped.
        Assert.Equal(WorkOrderStatus.InProcess, order.Status);
        Assert.Equal(PostingDate, order.ActualStartDate);

        // The transfer range-locks BOTH ends (it inserts into the WIP range too).
        var (lockedItems, lockedWarehouses) = Assert.Single(_stock.StockRangeLocks);
        Assert.Contains(_storesId, lockedWarehouses);
        Assert.Contains(_wipId, lockedWarehouses);
        Assert.Contains(_itemAId, lockedItems);
        Assert.Contains(_itemBId, lockedItems);
    }

    // ----------------------------------------------------------------------- rejection paths

    [Fact]
    public async Task Transfer_DraftOrder_FailsWithInvalidStatusTransitionAndWritesNothing()
    {
        var order = SeedOrder(WorkOrderStatus.Draft);

        var result = await Handler().HandleAsync(
            new TransferMaterialsToWipCommand(_companyId, order.Id, PostingDate), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.Draft, order.Status);
        Assert.Empty(_stock.StockEntries);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Transfer_UnknownOrder_FailsWithWorkOrderNotFoundAndWritesNothing()
    {
        var result = await Handler().HandleAsync(
            new TransferMaterialsToWipCommand(_companyId, Guid.NewGuid(), PostingDate), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.WorkOrderNotFound, result.Error!.Code);
        Assert.Empty(_stock.StockEntries);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Transfer_InsufficientStores_FailsWithInsufficientStockAndLeavesSubmitted()
    {
        var order = SeedOrder();
        _stock.SeedLedger(LedgerRow(_itemAId, _storesId, -18m, 15m)); // only 2xA left, 20 required

        var result = await Handler().HandleAsync(
            new TransferMaterialsToWipCommand(_companyId, order.Id, PostingDate), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(StockErrorCodes.InsufficientStock, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.Submitted, order.Status);
    }

    [Fact]
    public async Task Transfer_FrozenPeriod_FailsWithFiscalPeriodLockedAndWritesNothing()
    {
        _companies.Company!.FrozenAccountsDate = new DateOnly(2026, 12, 31);
        var order = SeedOrder();

        var result = await Handler().HandleAsync(
            new TransferMaterialsToWipCommand(_companyId, order.Id, PostingDate), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.Submitted, order.Status);
        Assert.Null(order.ActualStartDate);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }
}
