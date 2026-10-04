using Erp.Application.Features.Manufacturing.Commands;
using Erp.Application.Services;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 9.4 / spec MF-03: the manufacture completion exercised through the CQRS handler against
/// in-memory doubles - WIP consumption at FIFO cost, operating absorption from the BOM's planned
/// operations, the EXACT Dr 1330 $700 / Cr 1320 $500 / Cr 5210 $200 triple with the $70 unit rate,
/// the InProcess -&gt; Completed transition, and the guarantee that every rejection writes ZERO
/// rows (two-pass consumption: all FIFO reads land before the first write).
/// </summary>
public sealed class CompleteManufactureTests
{
    private static readonly DateOnly LayerDate = new(2026, 4, 1);
    private static readonly DateOnly PostingDate = new(2026, 4, 2);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _itemAId = Guid.NewGuid();
    private readonly Guid _itemBId = Guid.NewGuid();
    private readonly Guid _fgItemId = Guid.NewGuid();
    private readonly Guid _wipId = Guid.NewGuid();
    private readonly Guid _targetId = Guid.NewGuid();
    private readonly Guid _bomId = Guid.NewGuid();
    private readonly Guid _workstationId = Guid.NewGuid();

    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeWarehouseRepository _warehouses = new();
    private readonly FakeItemRepository _items = new();
    private readonly FakeStockRepository _stock = new();
    private readonly FakeManufacturingRepository _manufacturing = new();

    private readonly Account _wipAccount;
    private readonly Account _finishedAccount;
    private readonly Account _absorptionAccount;

    public CompleteManufactureTests()
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

        _wipAccount = NewAccount("1320", "Work In Progress Stock");
        _finishedAccount = NewAccount("1330", "Finished Goods Stock");
        _absorptionAccount = NewAccount("5210", "Expenses Included in Valuation");
        _accounts.Seed(_wipAccount, _finishedAccount, _absorptionAccount);
        _accounts.AccountsByCodeMap["5210"] = new[] { _absorptionAccount };

        _warehouses.Seed(
            NewWarehouse(_wipId, "WIP", _wipAccount.Id),
            NewWarehouse(_targetId, "FG", _finishedAccount.Id));

        _items.Seed(NewItem(_itemAId, "COMP-A"), NewItem(_itemBId, "COMP-B"), NewItem(_fgItemId, "FG-001"));

        // WS-01 composite rate: 25 labor + 10 electricity + 5 rent = $40/hour (spec MF-01).
        _manufacturing.SeedWorkstation(new Workstation
        {
            Id = _workstationId,
            TenantId = _tenantId,
            CompanyId = _companyId,
            WorkstationName = "WS-01",
            HourRateLabor = 25m,
            HourRateElectricity = 10m,
            HourRateRent = 5m,
            IsActive = true,
        });

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
            ScrapCost = 0m,
            Items =
            {
                new BomItem { Id = Guid.NewGuid(), ItemId = _itemAId, Quantity = 2m, ValuationRate = 15m, Amount = 30m },
                new BomItem { Id = Guid.NewGuid(), ItemId = _itemBId, Quantity = 1m, ValuationRate = 20m, Amount = 20m },
            },
            Operations =
            {
                new BomOperation
                {
                    Id = Guid.NewGuid(),
                    WorkstationId = _workstationId,
                    Description = "Assembly",
                    DurationMinutes = 30m,
                },
            },
        });

        // WIP on hand after the MF-02 transfer: 20xA @ $15 + 10xB @ $20 = $500.
        _stock.SeedLedger(
            LedgerRow(_itemAId, _wipId, +20m, 15m, "MT-2026-00001"),
            LedgerRow(_itemBId, _wipId, +10m, 20m, "MT-2026-00001"));
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
            Currency = "USD",
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

    private StockLedgerEntry LedgerRow(Guid itemId, Guid warehouseId, decimal qty, decimal rate, string voucherNo) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ItemId = itemId,
            WarehouseId = warehouseId,
            VoucherType = "StockEntry",
            VoucherNo = voucherNo,
            PostingDate = LayerDate,
            QtyChange = qty,
            ValuationRate = rate,
            Amount = qty * rate,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
        };

    private CompleteManufactureCommandHandler Handler() =>
        new(new ManufacturingPostingService(
            _companies, _accounts, _warehouses, _items, _stock, _manufacturing));

    private WorkOrder SeedOrder(WorkOrderStatus status = WorkOrderStatus.InProcess, decimal qty = 10m)
    {
        var order = new WorkOrder
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            OrderNumber = "WO-2026-00001",
            ProductionItemId = _fgItemId,
            BomId = _bomId,
            QuantityToProduce = qty,
            Status = status,
            SourceWarehouseId = Guid.NewGuid(),
            WipWarehouseId = _wipId,
            TargetWarehouseId = _targetId,
            PlannedStartDate = new DateOnly(2026, 4, 1),
            PlannedEndDate = new DateOnly(2026, 4, 10),
            ActualStartDate = new DateOnly(2026, 4, 2),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _manufacturing.SeedWorkOrder(order);
        return order;
    }

    // ------------------------------------------------------------------ happy path (spec MF-03)

    /// <summary>
    /// MF-03 verbatim: 10 assemblies complete from $500 of WIP + $200 of absorbed operations,
    /// so 10 units land in finished goods @ $70.00 ($700.00) with the exact three-leg voucher.
    /// </summary>
    [Fact]
    public async Task Complete_InProcessOrder_PostsExactTripleAndCompletes()
    {
        var order = SeedOrder();

        var result = await Handler().HandleAsync(
            new CompleteManufactureCommand(_companyId, order.Id, ProducedQuantity: 10m, PostingDate),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var posting = result.Value!;

        Assert.Equal(StockEntryType.Manufacture, posting.Entry.EntryType);
        Assert.Equal("MF-2026-00001", posting.Entry.VoucherNo);

        // Finished goods: +10 units @ $70.00 = $700.00.
        var fgLine = Assert.Single(posting.Entry.Lines);
        Assert.Equal(10m, fgLine.Qty);
        Assert.Equal(70m, fgLine.Rate);

        var fgRows = _stock.AddedLedger.Where(e => e.WarehouseId == _targetId).ToList();
        var fgRow = Assert.Single(fgRows);
        Assert.Equal(10m, fgRow.QtyChange);
        Assert.Equal(70m, fgRow.ValuationRate);
        Assert.Equal(700m, fgRow.Amount);

        // WIP relieved at FIFO cost: -20xA @ $15, -10xB @ $20 = -$500.
        Assert.Equal(-500m, _stock.AddedLedger.Where(e => e.WarehouseId == _wipId).Sum(e => e.Amount));

        // The EXACT MF-03 triple.
        var debit1330 = posting.GlEntries.Where(g => g.AccountCode == "1330").ToList();
        Assert.Equal(700m, debit1330.Sum(g => g.Debit));
        Assert.Equal(0m, debit1330.Sum(g => g.Credit));

        var credit1320 = posting.GlEntries.Where(g => g.AccountCode == "1320").ToList();
        Assert.Equal(500m, credit1320.Sum(g => g.Credit));
        Assert.Equal(0m, credit1320.Sum(g => g.Debit));

        var credit5210 = posting.GlEntries.Where(g => g.AccountCode == "5210").ToList();
        Assert.Equal(200m, credit5210.Sum(g => g.Credit));
        Assert.Equal(0m, credit5210.Sum(g => g.Debit));

        Assert.Equal(posting.TotalDebit, posting.TotalCredit);
        Assert.Equal(700m, posting.TotalDebit);

        // The order completed with production stamped.
        Assert.Equal(WorkOrderStatus.Completed, order.Status);
        Assert.Equal(10m, order.ProducedQuantity);
        Assert.Equal(PostingDate, order.ActualEndDate);

        // The completion range-locks WIP (consumed) AND the FG target (inserted).
        var (lockedItems, lockedWarehouses) = Assert.Single(_stock.StockRangeLocks);
        Assert.Contains(_wipId, lockedWarehouses);
        Assert.Contains(_targetId, lockedWarehouses);
        Assert.Contains(_itemAId, lockedItems);
        Assert.Contains(_itemBId, lockedItems);
        Assert.Contains(_fgItemId, lockedItems);
    }

    // ----------------------------------------------------------------------- rejection paths

    [Fact]
    public async Task Complete_SubmittedOrder_FailsWithInvalidStatusTransitionAndWritesNothing()
    {
        var order = SeedOrder(WorkOrderStatus.Submitted);

        var result = await Handler().HandleAsync(
            new CompleteManufactureCommand(_companyId, order.Id, 10m, PostingDate), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.Submitted, order.Status);
        Assert.Empty(_stock.StockEntries);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Complete_ZeroProducedQuantity_FailsAndWritesNothing()
    {
        var order = SeedOrder();

        var result = await Handler().HandleAsync(
            new CompleteManufactureCommand(_companyId, order.Id, 0m, PostingDate), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.InvalidWorkOrderQuantity, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.InProcess, order.Status);
        Assert.Empty(_stock.StockEntries);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Complete_OverProduction_FailsAndWritesNothing()
    {
        var order = SeedOrder(qty: 10m);

        var result = await Handler().HandleAsync(
            new CompleteManufactureCommand(_companyId, order.Id, 11m, PostingDate), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.InvalidWorkOrderQuantity, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.InProcess, order.Status);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Complete_InsufficientWip_FailsWithInsufficientStockAndWritesNothing()
    {
        var order = SeedOrder();
        _stock.SeedLedger(LedgerRow(_itemAId, _wipId, -15m, 15m, "MT-2026-00002")); // only 5xA left, 20 required

        var result = await Handler().HandleAsync(
            new CompleteManufactureCommand(_companyId, order.Id, 10m, PostingDate), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(StockErrorCodes.InsufficientStock, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.InProcess, order.Status);
        Assert.Equal(0m, order.ProducedQuantity);
        Assert.Empty(_stock.StockEntries);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Complete_FrozenPeriod_FailsWithFiscalPeriodLockedAndWritesNothing()
    {
        _companies.Company!.FrozenAccountsDate = new DateOnly(2026, 12, 31);
        var order = SeedOrder();

        var result = await Handler().HandleAsync(
            new CompleteManufactureCommand(_companyId, order.Id, 10m, PostingDate), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.InProcess, order.Status);
        Assert.Null(order.ActualEndDate);
        Assert.Empty(_stock.StockEntries);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Complete_ScrapBearingBom_FailsWithScrapValuationNotSupportedAndWritesNothing()
    {
        var scrapBomId = Guid.NewGuid();
        _manufacturing.SeedBom(new BillOfMaterials
        {
            Id = scrapBomId,
            TenantId = _tenantId,
            CompanyId = _companyId,
            BomNumber = "BOM-SCRAP",
            ItemId = _fgItemId,
            Quantity = 1m,
            IsActive = true,
            IsDefault = false,
            ScrapCost = 50m,
            Items =
            {
                new BomItem { Id = Guid.NewGuid(), ItemId = _itemAId, Quantity = 2m, ValuationRate = 15m, Amount = 30m },
            },
        });

        var order = SeedOrder();
        order.BomId = scrapBomId;

        var result = await Handler().HandleAsync(
            new CompleteManufactureCommand(_companyId, order.Id, 10m, PostingDate), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.ScrapValuationNotSupported, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.InProcess, order.Status);
        Assert.Empty(_stock.StockEntries);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Complete_MissingWorkstation_FailsWithWorkstationNotFoundAndWritesNothing()
    {
        var orphanBomId = Guid.NewGuid();
        _manufacturing.SeedBom(new BillOfMaterials
        {
            Id = orphanBomId,
            TenantId = _tenantId,
            CompanyId = _companyId,
            BomNumber = "BOM-ORPHAN",
            ItemId = _fgItemId,
            Quantity = 1m,
            IsActive = true,
            IsDefault = false,
            Items =
            {
                new BomItem { Id = Guid.NewGuid(), ItemId = _itemAId, Quantity = 2m, ValuationRate = 15m, Amount = 30m },
            },
            Operations =
            {
                new BomOperation { Id = Guid.NewGuid(), WorkstationId = Guid.NewGuid(), DurationMinutes = 30m },
            },
        });

        var order = SeedOrder();
        order.BomId = orphanBomId;

        var result = await Handler().HandleAsync(
            new CompleteManufactureCommand(_companyId, order.Id, 10m, PostingDate), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManufacturingErrorCodes.WorkstationNotFound, result.Error!.Code);
        Assert.Equal(WorkOrderStatus.InProcess, order.Status);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task Complete_ConcurrencyConflict_FailsWithConcurrencyConflict()
    {
        var order = SeedOrder();
        _manufacturing.FailNextWorkOrderUpdate = true;

        var result = await Handler().HandleAsync(
            new CompleteManufactureCommand(_companyId, order.Id, 10m, PostingDate), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("concurrency_conflict", result.Error!.Code);
    }
}
