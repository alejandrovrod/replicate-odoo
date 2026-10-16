using Erp.Application.Features.LandedCost.Commands;
using Erp.Application.Services;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Erp.Domain.Services;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Landed cost voucher Fase 4 (spec landed-cost-voucher, LCV-G1..LCV-G4): deterministic
/// apportionment by amount/quantity with exact cents, the inventory-uplift GL pair with a
/// strict balance, Kardex revaluation rows that lift future issue rates, draft-receipt
/// rejection and the compensating cancel - through the calc service and the LCV handlers.
/// </summary>
public sealed class LandedCostVoucherTests
{
    private static readonly DateOnly PostingDate = new(2026, 6, 15);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private readonly FakeLandedCostVoucherRepository _vouchers = new();
    private readonly FakePurchaseRepository _purchases = new();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeWarehouseRepository _warehouses = new();
    private readonly FakeItemRepository _items = new();
    private readonly FakeStockRepository _stock = new();

    private readonly Warehouse _warehouse;
    private readonly Item _itemA;
    private readonly Item _itemB;
    private readonly Account _inventory;
    private readonly Account _freight;
    private readonly PurchaseReceipt _receipt;

    public LandedCostVoucherTests()
    {
        _inventory = NewAccount("1104", "Inventario de Materias Primas", AccountRootType.Asset);
        _freight = NewAccount("5105", "Gastos de Flete", AccountRootType.Expense);
        var received = NewAccount("2120", "Stock Received But Not Billed", AccountRootType.Liability);
        var cogs = NewAccount("5210", "Cost of Goods Sold", AccountRootType.Expense);
        _accounts.Seed(_inventory, _freight, received, cogs);
        _accounts.AccountsByCodeMap["5210"] = new[] { cogs };

        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = _tenantId,
            Name = "Acme",
            AllowNegativeStock = false,
            StockReceivedAccountCode = "2120",
            CogsAccountCode = "5210",
        };

        _warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            WarehouseCode = "WH-01",
            WarehouseName = "Main Stores",
            AccountId = _inventory.Id,
        };
        _warehouses.Seed(_warehouse);

        _itemA = NewItem("INS-A", "Insumo A");
        _itemB = NewItem("INS-B", "Insumo B");
        _items.Seed(_itemA, _itemB);

        // LCV-G background: PREC-2026-0001, A 100x10 + B 50x20, submitted.
        _receipt = new PurchaseReceipt
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            VoucherNo = "PREC-2026-00001",
            SupplierId = Guid.NewGuid(),
            WarehouseId = _warehouse.Id,
            PostingDate = new DateOnly(2026, 6, 1),
            Status = PurchaseReceiptStatus.Submitted,
            TotalAmount = 2000m,
            CreatedAt = DateTimeOffset.UtcNow,
            Lines = new List<PurchaseReceiptLine>
            {
                new() { Id = Guid.NewGuid(), ItemId = _itemA.Id, Qty = 100m, Rate = 10m, Amount = 1000m, LineNumber = 1 },
                new() { Id = Guid.NewGuid(), ItemId = _itemB.Id, Qty = 50m, Rate = 20m, Amount = 1000m, LineNumber = 2 },
            },
        };
        foreach (var line in _receipt.Lines)
        {
            line.PurchaseReceiptId = _receipt.Id;
        }

        _purchases.SeedReceipt(_receipt);

        // The receipt's intake layers (what a real posting would have written).
        _stock.SeedLedger(
            Layer(_itemA.Id, 100m, 10m, new DateOnly(2026, 6, 1)),
            Layer(_itemB.Id, 50m, 20m, new DateOnly(2026, 6, 1)));
    }

    private Account NewAccount(string code, string name, AccountRootType root) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            AccountCode = code,
            AccountName = name,
            RootType = root,
            IsGroup = false,
            IsActive = true,
        };

    private Item NewItem(string code, string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ItemCode = code,
            ItemName = name,
            ValuationMethod = ValuationMethod.Fifo,
            StockUomId = Guid.NewGuid(),
        };

    private StockLedgerEntry Layer(Guid itemId, decimal qty, decimal rate, DateOnly date) =>
        new()
        {
            Id = Guid.NewGuid(),
            ItemId = itemId,
            WarehouseId = _warehouse.Id,
            PostingDate = date,
            QtyChange = qty,
            ValuationRate = rate,
            Amount = qty * rate,
            VoucherType = "PurchaseReceipt",
            VoucherNo = _receipt.VoucherNo,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private CreateLandedCostVoucherCommandHandler CreateHandler() =>
        new(_vouchers, _purchases, _companies, _accounts, _items);

    private SubmitLandedCostVoucherCommandHandler SubmitHandler() =>
        new(_vouchers, _purchases, _companies, _accounts, _warehouses, _items, _stock);

    private CancelLandedCostVoucherCommandHandler CancelHandler() =>
        new(_vouchers, _purchases, _companies, _accounts, _items, _stock);

    private static LandedCostChargeInput Freight(decimal amount, Guid accountId) =>
        new("Ocean freight", amount, accountId);

    // ------------------------------------------------------------------ calc (G1/G2)

    [Fact]
    public void Apportion_ByAmount_SplitsProportionallyWithExactCents()
    {
        var calc = new LandedCostCalculationService();
        var result = calc.Apportion(
            [
                new LandedCostApportionmentInput(Guid.NewGuid(), Guid.NewGuid(), _itemA.Id, 100m, 1000m, 10m),
                new LandedCostApportionmentInput(Guid.NewGuid(), Guid.NewGuid(), _itemB.Id, 50m, 1000m, 20m),
            ],
            200m,
            LandedCostDistributeBy.Amount);

        // LCV-G1: $100 each; new rates $11.00 and $22.00.
        Assert.Equal(100m, result[0].ApplicableCharges);
        Assert.Equal(11m, result[0].NewRate);
        Assert.Equal(100m, result[1].ApplicableCharges);
        Assert.Equal(22m, result[1].NewRate);
    }

    [Fact]
    public void Apportion_ByQuantity_SplitsPerUnit()
    {
        var calc = new LandedCostCalculationService();
        var result = calc.Apportion(
            [
                new LandedCostApportionmentInput(Guid.NewGuid(), Guid.NewGuid(), _itemA.Id, 100m, 1000m, 10m),
                new LandedCostApportionmentInput(Guid.NewGuid(), Guid.NewGuid(), _itemB.Id, 50m, 1000m, 20m),
            ],
            300m,
            LandedCostDistributeBy.Quantity);

        // LCV-G2: A (100u) $200 @ $2/u; B (50u) $100 @ $2/u.
        Assert.Equal(200m, result[0].ApplicableCharges);
        Assert.Equal(12m, result[0].NewRate);
        Assert.Equal(100m, result[1].ApplicableCharges);
        Assert.Equal(22m, result[1].NewRate);
    }

    [Fact]
    public void ApplyRevaluation_ReratesOpenLayersProportionally()
    {
        // $200 over 150 units: 100u layer absorbs 133.3333 -> 11.333333; the 50u layer
        // absorbs the residual 66.6667 -> 21.333334.
        var rerated = FifoValuation.BuildLayers(new[]
        {
            new StockLedgerEntry { QtyChange = 100m, ValuationRate = 10m, PostingDate = PostingDate, CreatedAt = DateTimeOffset.UtcNow },
            new StockLedgerEntry { QtyChange = 50m, ValuationRate = 20m, PostingDate = PostingDate.AddDays(1), CreatedAt = DateTimeOffset.UtcNow },
            new StockLedgerEntry { QtyChange = 0m, ValuationRate = 0m, Amount = 200m, PostingDate = PostingDate.AddDays(2), CreatedAt = DateTimeOffset.UtcNow },
        });

        Assert.Equal(11.333333m, rerated[0].Rate);
        Assert.Equal(21.333334m, rerated[1].Rate);

        // Consumption after the adjustment carries the landed cost...
        var consumed = FifoValuation.Consume(rerated, 100m, allowNegativeStock: false);
        Assert.Equal(1133.3333m, consumed.TotalCost);

        // ...while a zero-amount row stays a pure no-op (historical skip preserved).
        var noop = FifoValuation.BuildLayers(new[]
        {
            new StockLedgerEntry { QtyChange = 10m, ValuationRate = 5m, PostingDate = PostingDate, CreatedAt = DateTimeOffset.UtcNow },
            new StockLedgerEntry { QtyChange = 0m, ValuationRate = 0m, Amount = 0m, PostingDate = PostingDate, CreatedAt = DateTimeOffset.UtcNow },
        });
        Assert.Equal(5m, Assert.Single(noop).Rate);
    }

    // ------------------------------------------------------------ create/submit (G3/G4)

    [Fact]
    public async Task Create_WithDraftReceipt_RejectedWithNotSubmitted()
    {
        var draft = new PurchaseReceipt
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            VoucherNo = "PREC-DRAFT",
            SupplierId = Guid.NewGuid(),
            WarehouseId = _warehouse.Id,
            PostingDate = PostingDate,
            Status = PurchaseReceiptStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _purchases.SeedReceipt(draft);

        var result = await CreateHandler().HandleAsync(new CreateLandedCostVoucherCommand(
            _companyId,
            PostingDate,
            LandedCostDistributeBy.Amount,
            [draft.Id],
            [Freight(200m, _freight.Id)]));

        // LCV-G4: Draft (or Cancelled) receipts never absorb landed costs.
        Assert.False(result.IsSuccess);
        Assert.Equal(LandedCostErrorCodes.PurchaseReceiptNotSubmitted, result.Error!.Code);
        Assert.Empty(_vouchers.Vouchers);
    }

    [Fact]
    public async Task Submit_WithFreight_PostsExactLcvG3Ledger()
    {
        var created = await CreateHandler().HandleAsync(new CreateLandedCostVoucherCommand(
            _companyId,
            PostingDate,
            LandedCostDistributeBy.Amount,
            [_receipt.Id],
            [Freight(200m, _freight.Id)]));
        Assert.True(created.IsSuccess);
        Assert.Equal(DocumentStatus.Draft, created.Value!.Status);
        Assert.Equal(200m, created.Value.TotalTaxesAndCharges);
        Assert.Equal(2, created.Value.Items.Count);

        var submitted = await SubmitHandler().HandleAsync(
            new SubmitLandedCostVoucherCommand(_companyId, created.Value.Id));
        Assert.True(submitted.IsSuccess);
        Assert.Equal(DocumentStatus.Submitted, submitted.Value!.Status);
        Assert.Equal("LCV-2026-00001", submitted.Value.VoucherNo);

        // LCV-G3 exact table (2 lines, strictly balanced).
        var gls = _stock.AddedGlEntries.Where(g => g.VoucherId == submitted.Value.Id).ToList();
        Assert.Equal(2, gls.Count);
        var debit = Assert.Single(gls, g => g.Debit > 0);
        var credit = Assert.Single(gls, g => g.Credit > 0);
        Assert.Equal(_inventory.Id, debit.AccountId);
        Assert.Equal(200m, debit.Debit);
        Assert.Equal(_freight.Id, credit.AccountId);
        Assert.Equal(200m, credit.Credit);

        // Kardex: one zero-quantity revaluation row per receipt line (value up, qty intact).
        var sles = _stock.AddedLedger.Where(e => e.VoucherType == "LandedCostVoucher").ToList();
        Assert.Equal(2, sles.Count);
        Assert.All(sles, e => Assert.Equal(0m, e.QtyChange));
        Assert.Equal(200m, sles.Sum(e => e.Amount));
        Assert.Contains(sles, e => e.ItemId == _itemA.Id && e.ValuationRate == 11m);
        Assert.Contains(sles, e => e.ItemId == _itemB.Id && e.ValuationRate == 22m);
    }

    [Fact]
    public async Task IssueAfterSubmit_CarriesLandedCost()
    {
        var created = await CreateHandler().HandleAsync(new CreateLandedCostVoucherCommand(
            _companyId,
            PostingDate,
            LandedCostDistributeBy.Amount,
            [_receipt.Id],
            [Freight(200m, _freight.Id)]));
        Assert.True(created.IsSuccess);
        Assert.True((await SubmitHandler().HandleAsync(
            new SubmitLandedCostVoucherCommand(_companyId, created.Value!.Id))).IsSuccess);

        // Future issues consume the rerated layers: 10u of A cost 10 x $11.00.
        var stock = new StockPostingService(
            _companies, _accounts, _warehouses, _items, _stock);
        var posted = await stock.PostAsync(new StockPostingRequest(
            _companyId, StockEntryType.MaterialIssue, PostingDate.AddDays(1), _warehouse.Id, null,
            [new StockPostingLine(_itemA.Id, 10m, null)]));

        Assert.Equal(110m, posted.TotalCredit);
    }

    [Fact]
    public async Task Submit_WithoutOpenLayers_RejectedWithNoStockToRevalue()
    {
        // A receipt whose intake was fully sold holds no open layers: the revaluation
        // has nothing to attach to.
        var emptyReceipt = new PurchaseReceipt
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            VoucherNo = "PREC-2026-00002",
            SupplierId = Guid.NewGuid(),
            WarehouseId = _warehouse.Id,
            PostingDate = new DateOnly(2026, 6, 1),
            Status = PurchaseReceiptStatus.Submitted,
            TotalAmount = 500m,
            CreatedAt = DateTimeOffset.UtcNow,
            Lines = new List<PurchaseReceiptLine>
            {
                new() { Id = Guid.NewGuid(), ItemId = _itemA.Id, Qty = 50m, Rate = 10m, Amount = 500m, LineNumber = 1 },
            },
        };
        foreach (var line in emptyReceipt.Lines)
        {
            line.PurchaseReceiptId = emptyReceipt.Id;
        }

        _purchases.SeedReceipt(emptyReceipt);

        var created = await CreateHandler().HandleAsync(new CreateLandedCostVoucherCommand(
            _companyId,
            PostingDate,
            LandedCostDistributeBy.Amount,
            [emptyReceipt.Id],
            [Freight(100m, _freight.Id)]));
        Assert.True(created.IsSuccess);

        // Item A's seeded layers are fully consumed first via a stock issue.
        var stock = new StockPostingService(
            _companies, _accounts, _warehouses, _items, _stock);
        await stock.PostAsync(new StockPostingRequest(
            _companyId, StockEntryType.MaterialIssue, PostingDate, _warehouse.Id, null,
            [new StockPostingLine(_itemA.Id, 100m, null)]));

        var submitted = await SubmitHandler().HandleAsync(
            new SubmitLandedCostVoucherCommand(_companyId, created.Value!.Id));
        Assert.False(submitted.IsSuccess);
        Assert.Equal(LandedCostErrorCodes.NoStockToRevalue, submitted.Error!.Code);
    }

    [Fact]
    public async Task Cancel_ReversesLedgerAndRestoresRates()
    {
        var created = await CreateHandler().HandleAsync(new CreateLandedCostVoucherCommand(
            _companyId,
            PostingDate,
            LandedCostDistributeBy.Amount,
            [_receipt.Id],
            [Freight(200m, _freight.Id)]));
        Assert.True(created.IsSuccess);
        var submitted = await SubmitHandler().HandleAsync(
            new SubmitLandedCostVoucherCommand(_companyId, created.Value!.Id));
        Assert.True(submitted.IsSuccess);

        var cancelled = await CancelHandler().HandleAsync(
            new CancelLandedCostVoucherCommand(_companyId, submitted.Value!.Id));
        Assert.True(cancelled.IsSuccess);
        Assert.Equal(DocumentStatus.Cancelled, cancelled.Value!.Status);

        // Compensating rows: GL mirror + negated value rows under the same voucher number.
        var gls = _stock.AddedGlEntries.Where(g => g.VoucherId == submitted.Value.Id).ToList();
        Assert.Equal(4, gls.Count);
        Assert.Equal(gls.Sum(g => g.Debit), gls.Sum(g => g.Credit));

        var sles = _stock.AddedLedger.Where(e => e.VoucherNo == submitted.Value.VoucherNo).ToList();
        Assert.Equal(4, sles.Count);
        Assert.Equal(0m, sles.Sum(e => e.Amount)); // value fully restored

        // Replay after cancel prices issues at the ORIGINAL rates again.
        var layers = FifoValuation.BuildLayers(
            _stock.PersistedLedger.Where(e => e.ItemId == _itemA.Id && e.WarehouseId == _warehouse.Id));
        var consumed = FifoValuation.Consume(layers, 10m, allowNegativeStock: false);
        Assert.Equal(100m, consumed.TotalCost);
    }
}
