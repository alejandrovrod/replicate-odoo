using Erp.Application.Services;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Tasks 4.2/4.3 + spec BY-01: <see cref="PurchasePostingService"/> orchestration - the receipt
/// accrual (Dr warehouse stock / Cr 2120 + Kardex rows), the invoice clearance (Dr 2120 at receipt
/// value + Dr Input Tax + Dr/Cr price difference / Cr Accounts Payable), the full three-way match
/// rejections and the order workflow transitions - against in-memory repository doubles, so no EF
/// Core and no database are involved (Constitution I.2/I.3).
/// </summary>
public sealed class PurchasePostingServiceTests
{
    private static readonly DateOnly PostingDate = new(2026, 3, 2);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeWarehouseRepository _warehouses = new();
    private readonly FakeItemRepository _items = new();
    private readonly FakeStockRepository _stock = new();
    private readonly FakePurchaseRepository _purchases = new();

    private readonly Account _stockAccount;    // 1310 - Stock In Hand (warehouse asset)
    private readonly Account _receivedAccount; // 2120 - Stock Received But Not Billed (interim)
    private readonly Account _payableAccount;  // 2110 - Accounts Payable (vendor gross)
    private readonly Account _taxAccount;      // 1130 - Input Tax Recoverable (spec BY-01)
    private readonly Account _diffAccount;     // 5120 - Purchase Price Difference (variances)
    private readonly Warehouse _warehouse;
    private readonly Item _item;
    private readonly Supplier _supplier;

    public PurchasePostingServiceTests()
    {
        _stockAccount = NewAccount("1310", "Stock In Hand");
        _receivedAccount = NewAccount("2120", "Stock Received But Not Billed");
        _payableAccount = NewAccount("2110", "Accounts Payable");
        _taxAccount = NewAccount("1130", "Input Tax Recoverable");
        _diffAccount = NewAccount("5120", "Purchase Price Difference");
        _accounts.Seed(_stockAccount, _receivedAccount, _payableAccount, _taxAccount, _diffAccount);

        // Decision D3: company defaults are account CODES resolved to one active leaf each.
        _accounts.AccountsByCodeMap["2120"] = new[] { _receivedAccount };
        _accounts.AccountsByCodeMap["2110"] = new[] { _payableAccount };
        _accounts.AccountsByCodeMap["1130"] = new[] { _taxAccount };
        _accounts.AccountsByCodeMap["5120"] = new[] { _diffAccount };

        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = _tenantId,
            Name = "Acme Industrial",
            AllowNegativeStock = false,
            StockReceivedAccountCode = "2120",
            AccountsPayableAccountCode = "2110",
            InputTaxRecoverableAccountCode = "1130",
            PriceDifferenceAccountCode = "5120",
        };

        _warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            Code = "WH-01",
            Name = "Main Stores",
            StockAccountId = _stockAccount.Id,
        };
        _warehouses.Seed(_warehouse);

        _item = new Item
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Code = "IT-001",
            Name = "Steel Bracket",
            ValuationMethod = ValuationMethod.Fifo,
            BaseUOMId = Guid.NewGuid(),
        };
        _items.Seed(_item);

        _supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Code = "SUP-001",
            Name = "Acme Industrial Supplies",
            IsActive = true,
        };
    }

    private PurchasePostingService CreateService() =>
        new(_companies, _accounts, _warehouses, _items, _stock, _purchases);

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

    private PurchaseOrder SeedOrder(PurchaseOrderStatus status)
    {
        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            CompanyId = _companyId,
            SupplierId = _supplier.Id,
            Status = status,
            PostingDate = PostingDate.AddDays(-7),
            VoucherNo = "PO-2026-00001",
            CreatedAt = DateTimeOffset.UtcNow,
            Lines = new List<PurchaseOrderLine>
            {
                new() { Id = Guid.NewGuid(), ItemId = _item.Id, Qty = 10m, Rate = 100m, LineNumber = 1 },
            },
        };
        _purchases.SeedOrder(order);
        return order;
    }

    // The service reads company/warehouse from the request arguments, so build them per test:
    private PurchaseReceiptPostingRequest NewReceiptRequest(Guid? purchaseOrderId = null) =>
        new(_companyId, _warehouse.Id, purchaseOrderId, PostingDate,
            new[] { new PurchaseReceiptPostingLine(_item.Id, 10m, 100m) });

    /// <summary>Posts the standard receipt (10 units @ $100) and returns the persisted aggregate.</summary>
    private async Task<PurchaseReceipt> PostStandardReceiptAsync(Guid? purchaseOrderId = null)
    {
        await CreateService().PostReceiptAsync(NewReceiptRequest(purchaseOrderId));
        return _purchases.Receipts.Single();
    }

    private static PurchaseInvoicePostingLine MatchLine(PurchaseReceiptLine line, decimal rate) =>
        new(line.Id, line.ItemId, line.Qty, rate);

    private PurchaseInvoicePostingRequest NewInvoiceRequest(
        Guid receiptId, decimal taxAmount, params PurchaseInvoicePostingLine[] lines) =>
        new(_companyId, receiptId, PostingDate, taxAmount, lines);

    // ------------------------------------------------------------------------ receipt (task 4.2)

    [Fact]
    public async Task PostReceiptAsync_SingleLine_WritesBalancedAccrualAndKardexRows()
    {
        var result = await CreateService().PostReceiptAsync(NewReceiptRequest());

        // Task 4.2: gapless PR-YYYY-NNNNN voucher, first receipt of the year.
        Assert.Equal("PR-2026-00001", result.Receipt.VoucherNo);

        // Balanced accrual: Dr 1310 Stock In Hand $1,000 / Cr 2120 SRNB $1,000 (ST-01 shape).
        Assert.Equal(1000m, result.TotalDebit);
        Assert.Equal(1000m, result.TotalCredit);
        Assert.Equal(2, result.GlEntries.Count);
        var debit = result.GlEntries.Single(g => g.AccountCode == "1310");
        Assert.Equal(1000m, debit.Debit);
        Assert.Equal(0m, debit.Credit);
        var credit = result.GlEntries.Single(g => g.AccountCode == "2120");
        Assert.Equal(0m, credit.Debit);
        Assert.Equal(1000m, credit.Credit);
        Assert.All(result.GlEntries, g =>
        {
            Assert.Equal("PurchaseReceipt", g.VoucherType);
            Assert.Equal("PR-2026-00001", g.VoucherNo);
        });

        // Kardex: +10 units valued at the receipt rate, stamped with the voucher provenance.
        var ledger = Assert.Single(result.LedgerEntries);
        Assert.Equal(10m, ledger.QtyChange);
        Assert.Equal(100m, ledger.ValuationRate);
        Assert.Equal(1000m, ledger.Amount);
        var ledgerRow = _stock.AddedLedger.Single();
        Assert.Equal("PurchaseReceipt", ledgerRow.VoucherType);
        Assert.Equal("PR-2026-00001", ledgerRow.VoucherNo);
        Assert.Null(ledgerRow.StockEntryId);

        // ONE transaction wraps validation + Kardex + GL + number + save.
        Assert.Equal(1, _purchases.TransactionCount);

        var receipt = Assert.Single(_purchases.Receipts);
        Assert.Equal(_warehouse.Id, receipt.WarehouseId);
        var receiptLine = Assert.Single(receipt.Lines);
        Assert.Equal(_item.Id, receiptLine.ItemId);
        Assert.Equal(10m, receiptLine.Qty);
        Assert.Equal(100m, receiptLine.Rate);
    }

    [Fact]
    public async Task PostReceiptAsync_SecondReceipt_GetsNextGaplessVoucher()
    {
        await CreateService().PostReceiptAsync(NewReceiptRequest());
        await CreateService().PostReceiptAsync(NewReceiptRequest());

        Assert.Equal("PR-2026-00001", _purchases.Receipts[0].VoucherNo);
        Assert.Equal("PR-2026-00002", _purchases.Receipts[1].VoucherNo);
    }

    [Fact]
    public async Task PostReceiptAsync_OrderedOrder_AdvancesWorkflowToReceived()
    {
        var order = SeedOrder(PurchaseOrderStatus.Ordered);

        var result = await CreateService().PostReceiptAsync(NewReceiptRequest(order.Id));

        Assert.Equal(PurchaseOrderStatus.Received, result.OrderStatus);
        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
        Assert.Equal(order.Id, result.Receipt.PurchaseOrderId);
    }

    [Theory]
    [InlineData(PurchaseOrderStatus.Draft)]
    [InlineData(PurchaseOrderStatus.Billed)]
    public async Task PostReceiptAsync_OrderNotReceivable_FailsWithInvalidStatusTransition(
        PurchaseOrderStatus status)
    {
        var order = SeedOrder(status);

        var ex = await Assert.ThrowsAsync<PurchaseValidationException>(() =>
            CreateService().PostReceiptAsync(NewReceiptRequest(order.Id)));

        Assert.Equal(PurchaseErrorCodes.InvalidStatusTransition, ex.Code);

        // Nothing was written: no receipt, no Kardex, no GL, order untouched.
        Assert.Empty(_purchases.Receipts);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
        Assert.Equal(status, order.Status);
    }

    [Fact]
    public async Task PostReceiptAsync_MissingCompanyAccrualCode_FailsWithConfigurationException()
    {
        _companies.Company!.StockReceivedAccountCode = null;

        await Assert.ThrowsAsync<PurchasePostingConfigurationException>(() =>
            CreateService().PostReceiptAsync(NewReceiptRequest()));

        Assert.Empty(_purchases.Receipts);
        Assert.Empty(_stock.AddedLedger);
    }

    [Fact]
    public async Task PostReceiptAsync_WarehouseWithoutStockAccount_FailsWithConfigurationException()
    {
        _warehouse.StockAccountId = Guid.Empty;

        await Assert.ThrowsAsync<PurchasePostingConfigurationException>(() =>
            CreateService().PostReceiptAsync(NewReceiptRequest()));

        Assert.Empty(_purchases.Receipts);
    }

    // ------------------------------------------------------------------------- invoice (task 4.3)

    [Fact]
    public async Task PostInvoiceAsync_By01_ClearsAccrualBooksTaxAndPayable()
    {
        // BY-01 arrange: the receipt accrued SRNB at $1,000 (10 @ $100).
        var order = SeedOrder(PurchaseOrderStatus.Ordered);
        var receipt = await PostStandardReceiptAsync(order.Id);
        var receiptLine = receipt.Lines.Single();

        var request = NewInvoiceRequest(
            receipt.Id, taxAmount: 100m, MatchLine(receiptLine, rate: 100m));

        var result = await CreateService().PostInvoiceAsync(request);

        // spec BY-01 exactly: Dr 2120 $1,000 + Dr 1130 $100 / Cr 2110 $1,100.
        Assert.Equal(1100m, result.TotalDebit);
        Assert.Equal(1100m, result.TotalCredit);
        Assert.Equal(3, result.GlEntries.Count);
        var interim = result.GlEntries.Single(g => g.AccountCode == "2120");
        Assert.Equal(1000m, interim.Debit);
        Assert.Equal(0m, interim.Credit);
        var tax = result.GlEntries.Single(g => g.AccountCode == "1130");
        Assert.Equal(100m, tax.Debit);
        Assert.Equal(0m, tax.Credit);
        var payable = result.GlEntries.Single(g => g.AccountCode == "2110");
        Assert.Equal(0m, payable.Debit);
        Assert.Equal(1100m, payable.Credit);

        Assert.Equal("PINV-2026-00001", result.Invoice.VoucherNo);
        Assert.All(result.GlEntries, g =>
        {
            Assert.Equal("PurchaseInvoice", g.VoucherType);
            Assert.Equal("PINV-2026-00001", g.VoucherNo);
        });
        Assert.Equal(100m, result.Invoice.TaxAmount);

        // Task 4.1: billing the receipt closes the order.
        Assert.Equal(PurchaseOrderStatus.Billed, result.OrderStatus);
        Assert.Equal(PurchaseOrderStatus.Billed, order.Status);

        Assert.Single(_purchases.Invoices);

        // ONE transaction per posting: receipt + invoice.
        Assert.Equal(2, _purchases.TransactionCount);
    }

    [Fact]
    public async Task PostInvoiceAsync_BilledAboveReceivedRate_DebitsPriceDifference()
    {
        var receipt = await PostStandardReceiptAsync();
        var receiptLine = receipt.Lines.Single();

        var request = NewInvoiceRequest(receipt.Id, taxAmount: 0m, MatchLine(receiptLine, rate: 110m));
        var result = await CreateService().PostInvoiceAsync(request);

        // Receipt value $1,000 clears 2120; billed $1,100 goes to AP; $100 variance to 5120.
        Assert.Equal(1100m, result.TotalDebit);
        Assert.Equal(1100m, result.TotalCredit);
        Assert.Equal(3, result.GlEntries.Count);
        Assert.Equal(1000m, result.GlEntries.Single(g => g.AccountCode == "2120").Debit);
        Assert.Equal(100m, result.GlEntries.Single(g => g.AccountCode == "5120").Debit);
        Assert.Equal(1100m, result.GlEntries.Single(g => g.AccountCode == "2110").Credit);
        Assert.DoesNotContain(result.GlEntries, g => g.AccountCode == "1130"); // no tax requested
    }

    [Fact]
    public async Task PostInvoiceAsync_BilledBelowReceivedRate_CreditsPriceDifference()
    {
        var receipt = await PostStandardReceiptAsync();
        var receiptLine = receipt.Lines.Single();

        var request = NewInvoiceRequest(receipt.Id, taxAmount: 0m, MatchLine(receiptLine, rate: 90m));
        var result = await CreateService().PostInvoiceAsync(request);

        // Receipt value $1,000 clears 2120; billed only $900; the $100 saving credits 5120.
        Assert.Equal(1000m, result.TotalDebit);
        Assert.Equal(1000m, result.TotalCredit);
        Assert.Equal(3, result.GlEntries.Count);
        Assert.Equal(1000m, result.GlEntries.Single(g => g.AccountCode == "2120").Debit);
        Assert.Equal(100m, result.GlEntries.Single(g => g.AccountCode == "5120").Credit);
        Assert.Equal(900m, result.GlEntries.Single(g => g.AccountCode == "2110").Credit);
    }

    [Fact]
    public async Task PostInvoiceAsync_QuantityAboveReceived_FailsWithQuantityMismatch()
    {
        var receipt = await PostStandardReceiptAsync();
        var receiptLine = receipt.Lines.Single();

        var request = NewInvoiceRequest(
            receipt.Id, taxAmount: 0m,
            new PurchaseInvoicePostingLine(receiptLine.Id, receiptLine.ItemId, 5m, 100m));

        var ex = await Assert.ThrowsAsync<PurchaseValidationException>(() =>
            CreateService().PostInvoiceAsync(request));

        Assert.Equal(PurchaseErrorCodes.QuantityMismatch, ex.Code);
        Assert.Empty(_purchases.Invoices);
        Assert.Equal(2, _purchases.TransactionCount); // receipt + the rolled-back invoice attempt
    }

    [Fact]
    public async Task PostInvoiceAsync_ForeignReceiptLine_FailsWithReceiptLineMismatch()
    {
        var receipt = await PostStandardReceiptAsync();

        var request = NewInvoiceRequest(
            receipt.Id, taxAmount: 0m,
            new PurchaseInvoicePostingLine(Guid.NewGuid(), _item.Id, 10m, 100m));

        var ex = await Assert.ThrowsAsync<PurchaseValidationException>(() =>
            CreateService().PostInvoiceAsync(request));

        Assert.Equal(PurchaseErrorCodes.ReceiptLineMismatch, ex.Code);
        Assert.Empty(_purchases.Invoices);
    }

    [Fact]
    public async Task PostInvoiceAsync_UncoveredReceiptLine_FailsWithReceiptLineMismatch()
    {
        // Receipt with TWO lines; the invoice bills only the first one (partial match).
        var twoLineRequest = new PurchaseReceiptPostingRequest(
            _companyId, _warehouse.Id, null, PostingDate,
            new[]
            {
                new PurchaseReceiptPostingLine(_item.Id, 10m, 100m),
                new PurchaseReceiptPostingLine(_item.Id, 5m, 50m),
            });
        await CreateService().PostReceiptAsync(twoLineRequest);
        var receipt = _purchases.Receipts.Single();

        var request = NewInvoiceRequest(
            receipt.Id, taxAmount: 0m, MatchLine(receipt.Lines.First(), rate: 100m));

        var ex = await Assert.ThrowsAsync<PurchaseValidationException>(() =>
            CreateService().PostInvoiceAsync(request));

        Assert.Equal(PurchaseErrorCodes.ReceiptLineMismatch, ex.Code);
        Assert.Empty(_purchases.Invoices);
    }

    [Fact]
    public async Task PostInvoiceAsync_AlreadyInvoiced_FailsWithInvoiceAlreadyExists()
    {
        var receipt = await PostStandardReceiptAsync();
        var receiptLine = receipt.Lines.Single();

        await CreateService().PostInvoiceAsync(
            NewInvoiceRequest(receipt.Id, taxAmount: 0m, MatchLine(receiptLine, rate: 100m)));

        var ex = await Assert.ThrowsAsync<PurchaseValidationException>(() =>
            CreateService().PostInvoiceAsync(
                NewInvoiceRequest(receipt.Id, taxAmount: 0m, MatchLine(receiptLine, rate: 100m))));

        Assert.Equal(PurchaseErrorCodes.InvoiceAlreadyExists, ex.Code);
        Assert.Single(_purchases.Invoices); // one invoice per receipt (unique index backstop)
    }

    [Fact]
    public async Task PostInvoiceAsync_NegativeTax_FailsBeforeAnyWrite()
    {
        var receipt = await PostStandardReceiptAsync();
        var receiptLine = receipt.Lines.Single();

        var ex = await Assert.ThrowsAsync<PurchaseValidationException>(() =>
            CreateService().PostInvoiceAsync(
                NewInvoiceRequest(receipt.Id, taxAmount: -1m, MatchLine(receiptLine, rate: 100m))));

        Assert.Equal(PurchaseErrorCodes.InvalidTaxAmount, ex.Code);
        Assert.Empty(_purchases.Invoices);
        Assert.Equal(1, _purchases.TransactionCount); // rejected before the transaction opened
    }

    [Fact]
    public async Task PostInvoiceAsync_MissingPayableCode_FailsWithConfigurationException()
    {
        var receipt = await PostStandardReceiptAsync();
        var receiptLine = receipt.Lines.Single();
        _companies.Company!.AccountsPayableAccountCode = null;

        await Assert.ThrowsAsync<PurchasePostingConfigurationException>(() =>
            CreateService().PostInvoiceAsync(
                NewInvoiceRequest(receipt.Id, taxAmount: 0m, MatchLine(receiptLine, rate: 100m))));

        Assert.Empty(_purchases.Invoices);
    }
}
