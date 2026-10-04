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
            TransactionDate = PostingDate.AddDays(-7),
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

    // The service reads company/warehouse from the request arguments, so build them per test:
    private PurchaseReceiptPostingRequest NewReceiptRequest(Guid? purchaseOrderId = null) =>
        new(_companyId, _warehouse.Id, _supplier.Id, purchaseOrderId, PostingDate,
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
        new(_companyId, _supplier.Id, "BILL-001", PostingDate, PostingDate.AddDays(30), taxAmount, lines);

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
        var order = SeedOrder(PurchaseOrderStatus.Submitted);

        var result = await CreateService().PostReceiptAsync(NewReceiptRequest(order.Id));

        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, result.OrderStatus);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);
        Assert.Equal(order.Id, result.Receipt.PurchaseOrderId);
    }

    [Fact]
    public async Task PostReceiptAsync_ConcurrentOrderUpdate_ThrowsConcurrencyConflict()
    {
        // Spec BY-06: the workflow transition is a read-modify-write, so a RowVersion mismatch on
        // save surfaces as a typed domain exception that bubbles to the CQRS handler (which turns
        // it into Result.Failure -> 409). The service must NOT swallow it.
        var order = SeedOrder(PurchaseOrderStatus.Submitted);
        _purchases.FailNextOrderUpdate = true;

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            CreateService().PostReceiptAsync(NewReceiptRequest(order.Id)));

        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, ex.Code);
        Assert.Equal(order.Id, ex.EntityId);
    }

    [Theory]
    [InlineData(PurchaseOrderStatus.Draft)]
    [InlineData(PurchaseOrderStatus.Completed)]
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
        _warehouse.AccountId = Guid.Empty;

        await Assert.ThrowsAsync<PurchasePostingConfigurationException>(() =>
            CreateService().PostReceiptAsync(NewReceiptRequest()));

        Assert.Empty(_purchases.Receipts);
    }

    // ------------------------------------------------------------------------- invoice (task 4.3)

    [Fact]
    public async Task PostInvoiceAsync_By01_ClearsAccrualBooksTaxAndPayable()
    {
        // BY-01 arrange: the receipt accrued SRNB at $1,000 (10 @ $100).
        var order = SeedOrder(PurchaseOrderStatus.Submitted);
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
        Assert.Equal(100m, result.Invoice.TaxTotal);

        // Spec BY-05: a posted bill is born Unpaid - that is the state cancellation transitions
        // FROM (Unpaid -> Cancelled), so the payable can be reversed later.
        Assert.Equal(PurchaseInvoiceStatus.Unpaid, result.Invoice.Status);
        Assert.Equal(1100m, result.Invoice.OutstandingAmount);

        // Task 4.1: billing the receipt closes the order.
        Assert.Equal(PurchaseOrderStatus.Completed, result.OrderStatus);
        Assert.Equal(PurchaseOrderStatus.Completed, order.Status);

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
    public async Task PostInvoiceAsync_QuantityAboveReceived_FailsWithOverbillingNotAllowed()
    {
        var receipt = await PostStandardReceiptAsync();
        var receiptLine = receipt.Lines.Single();

        var request = NewInvoiceRequest(
            receipt.Id, taxAmount: 0m,
            new PurchaseInvoicePostingLine(receiptLine.Id, receiptLine.ItemId, 15m, 100m));

        var ex = await Assert.ThrowsAsync<OverbillingNotAllowedException>(() =>
            CreateService().PostInvoiceAsync(request));

        // spec BY-03 literal: nothing billed yet, so the ceiling is the received quantity.
        Assert.Equal("Cannot bill 15 units. Maximum receivable: 10", ex.Message);

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
            _companyId, _warehouse.Id, _supplier.Id, null, PostingDate,
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
    public async Task PostInvoiceAsync_BillOnFullyBilledReceipt_FailsWithOverbillingNotAllowed()
    {
        var receipt = await PostStandardReceiptAsync();
        var receiptLine = receipt.Lines.Single();

        await CreateService().PostInvoiceAsync(
            NewInvoiceRequest(receipt.Id, taxAmount: 0m, MatchLine(receiptLine, rate: 100m)));

        var ex = await Assert.ThrowsAsync<OverbillingNotAllowedException>(() =>
            CreateService().PostInvoiceAsync(
                NewInvoiceRequest(receipt.Id, taxAmount: 0m, MatchLine(receiptLine, rate: 100m))));

        // spec BY-06 form: the receipt is fully billed, so nothing remains for the second bill.
        Assert.Equal("Only 0 units remaining to bill, 10 requested", ex.Message);

        Assert.Single(_purchases.Invoices); // the second bill is rejected by the cumulative validator
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

    // ------------------------------------------------ fiscal period lock (task 2.2 / AC-04)

    [Fact]
    public async Task PostReceiptAsync_BackDatedAgainstFrozenCompany_ThrowsFiscalPeriodLockAndWritesNothing()
    {
        // spec AC-04 Gherkin: freeze 2025-12-31, the receipt attempts to post on 2025-12-15.
        _companies.Company!.FrozenAccountsDate = new DateOnly(2025, 12, 31);

        var request = new PurchaseReceiptPostingRequest(
            _companyId, _warehouse.Id, _supplier.Id, null, new DateOnly(2025, 12, 15),
            new[] { new PurchaseReceiptPostingLine(_item.Id, 10m, 100m) });

        var ex = await Assert.ThrowsAsync<FiscalPeriodLockedException>(
            () => CreateService().PostReceiptAsync(request));

        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, ex.Code);
        Assert.Equal(new DateOnly(2025, 12, 15), ex.PostingDate);
        Assert.Equal(new DateOnly(2025, 12, 31), ex.FrozenAccountsDate);

        // AC-04: "no data is modified" - the domain check runs before the FIRST line is built.
        Assert.Empty(_purchases.Receipts);
        Assert.Empty(_stock.AddedLedger);
        Assert.Empty(_stock.AddedGlEntries);
    }

    [Fact]
    public async Task PostInvoiceAsync_PostingDateOnFreezeDate_ThrowsFiscalPeriodLockAndAppendsNoGlRows()
    {
        // Boundary at the SERVICE level: freeze ON the posting day (the "<=" of spec AC-04).
        var receipt = await PostStandardReceiptAsync();
        var glBefore = _stock.AddedGlEntries.Count;
        _companies.Company!.FrozenAccountsDate = receipt.PostingDate;

        var request = new PurchaseInvoicePostingRequest(
            _companyId, _supplier.Id, "INV-001", receipt.PostingDate, receipt.PostingDate.AddDays(30), 0m,
            new[] { MatchLine(receipt.Lines.Single(), rate: 100m) });

        var ex = await Assert.ThrowsAsync<FiscalPeriodLockedException>(
            () => CreateService().PostInvoiceAsync(request));

        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, ex.Code);
        Assert.Empty(_purchases.Invoices);

        // AC-04: zero GLEntry rows appended by the rejected attempt.
        Assert.Equal(glBefore, _stock.AddedGlEntries.Count);
    }

    // ---------------------------------------------- double-entry invariant (task 2.1 / AC-01)

    [Fact]
    public async Task PurchasePostings_SatisfyDoubleEntryZeroSumInvariant()
    {
        // Task 2.1 acceptance AT THE SERVICE LEVEL: the busiest GL paths - the receipt accrual
        // and the BY-01 invoice with input tax AND a price variance - must each obey
        // spec AC-01 |sum D - sum C| <= 0.0001. The THROW side lives in DoubleEntryGuardTests;
        // an imbalanced line set is structurally unreachable here (every pair derives from ONE
        // rounded amount) - it becomes reachable with the user-authored lines of tasks.md 2.3.
        var service = CreateService();
        var receiptPosting = await service.PostReceiptAsync(NewReceiptRequest());
        var receipt = _purchases.Receipts.Single();

        var invoiceRequest = new PurchaseInvoicePostingRequest(
            _companyId, _supplier.Id, "INV-001", PostingDate, PostingDate.AddDays(30), 100m,
            new[] { MatchLine(receipt.Lines.Single(), rate: 120m) });
        var invoicePosting = await service.PostInvoiceAsync(invoiceRequest);

        Assert.True(
            Math.Abs(receiptPosting.TotalDebit - receiptPosting.TotalCredit) <= 0.0001m,
            $"Receipt {receiptPosting.Receipt.VoucherNo} is out of balance: "
            + $"D={receiptPosting.TotalDebit:0.0000}, C={receiptPosting.TotalCredit:0.0000}.");
        Assert.True(
            Math.Abs(invoicePosting.TotalDebit - invoicePosting.TotalCredit) <= 0.0001m,
            $"Invoice {invoicePosting.Invoice.VoucherNo} is out of balance: "
            + $"D={invoicePosting.TotalDebit:0.0000}, C={invoicePosting.TotalCredit:0.0000}.");
    }
}


