using Erp.Application.Features.Buying.Commands;
using Erp.Application.Services;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Module 19 (returns-and-credit-notes, T1/T2/T3/T4 - purchase side): supplier debit notes posted
/// through <see cref="PurchasePostingService.PostReturnAsync"/> - the strict inverse GL
/// (Dr Accounts Payable / Cr Input Tax / Cr inventory or Cr accrued liability), the negative Kardex
/// rows on <c>UpdateStock</c>, the gapless PRET voucher, the per-line and cumulative ceilings and the
/// amortization of the referenced bill - against in-memory repository doubles (no EF, no database).
/// </summary>
public sealed class PurchaseReturnsTests
{
    private static readonly DateOnly PostingDate = new(2026, 4, 6);
    private static readonly DateOnly DueDate = PostingDate.AddDays(30);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeWarehouseRepository _warehouses = new();
    private readonly FakeItemRepository _items = new();
    private readonly FakeStockRepository _stock = new();
    private readonly FakePurchaseRepository _purchases = new();
    private readonly FakeExchangeRateRepository _exchangeRates = new();
    private readonly FakeAssetsRepository _assets = new();

    private readonly Account _stockAccount;    // 1310 - Stock In Hand (warehouse asset)
    private readonly Account _receivedAccount; // 2120 - Stock Received But Not Billed (interim)
    private readonly Account _payableAccount;  // 2110 - Accounts Payable (vendor gross)
    private readonly Account _taxAccount;      // 1130 - Input Tax Recoverable
    private readonly Warehouse _warehouse;
    private readonly Item _item;
    private readonly Supplier _supplier;

    public PurchaseReturnsTests()
    {
        _stockAccount = NewAccount("1310", "Stock In Hand");
        _receivedAccount = NewAccount("2120", "Stock Received But Not Billed");
        _payableAccount = NewAccount("2110", "Accounts Payable");
        _taxAccount = NewAccount("1130", "Input Tax Recoverable");
        _accounts.Seed(_stockAccount, _receivedAccount, _payableAccount, _taxAccount);
        _accounts.AccountsByCodeMap["2120"] = new[] { _receivedAccount };
        _accounts.AccountsByCodeMap["2110"] = new[] { _payableAccount };
        _accounts.AccountsByCodeMap["1130"] = new[] { _taxAccount };

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
        new(_companies, _accounts, _warehouses, _items, _stock, _purchases, _exchangeRates, _assets);

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

    /// <summary>
    /// Posts the standard original bill end-to-end (receipt 10 @ $100, then a full bill at the same
    /// rate) and returns the persisted invoice aggregate.
    /// </summary>
    private async Task<PurchaseInvoice> PostOriginalBillAsync(decimal taxAmount = 0m)
    {
        var service = CreateService();
        await service.PostReceiptAsync(new(
            _companyId, _warehouse.Id, _supplier.Id, null, PostingDate,
            new[] { new PurchaseReceiptPostingLine(_item.Id, 10m, 100m) }));

        var receiptLine = _purchases.Receipts.Single().Lines.Single();
        await service.PostInvoiceAsync(new(
            _companyId, _supplier.Id, "BILL-001", null, PostingDate, DueDate, taxAmount,
            new[] { new PurchaseInvoicePostingLine(receiptLine.Id, _item.Id, 10m, 100m) }));

        return _purchases.Invoices[^1];
    }

    private PurchaseReturnPostingRequest NewReturnRequest(
        Guid originalId,
        Guid originalLineId,
        decimal qty,
        decimal taxAmount = 0m,
        bool updateStock = false) =>
        new(_companyId, originalId, "", null, PostingDate, DueDate, taxAmount, updateStock,
            new[] { new PurchaseReturnPostingLine(originalLineId, qty) });

    // ------------------------------------------------------------------- post (T3/T4 + D3)

    [Fact]
    public async Task PostReturnAsync_PartialReturnWithStock_BooksInverseGlNegativeKardexAndAmortizes()
    {
        var original = await PostOriginalBillAsync();
        Assert.Equal(1000m, original.OutstandingAmount);

        var result = await CreateService().PostReturnAsync(
            NewReturnRequest(original.Id, original.Lines.Single().Id, qty: 4m, updateStock: true));

        // Gapless PRET voucher and return provenance.
        Assert.Equal("PRET-2026-00001", result.Invoice.VoucherNo);
        Assert.True(result.Invoice.IsReturn);
        Assert.Equal(original.Id, result.Invoice.ReturnAgainstId);
        Assert.True(result.Invoice.UpdateStock);

        // Negative mirror totals (module 19 T1).
        Assert.Equal(-400m, result.Invoice.NetTotal);
        Assert.Equal(0m, result.Invoice.TaxTotal);
        Assert.Equal(-400m, result.Invoice.GrandTotal);
        Assert.Equal(-400m, result.Invoice.OutstandingAmount);

        // Inverse GL: Dr Accounts Payable / Cr Stock In Hand; balanced.
        Assert.Equal(2, result.GlEntries.Count);
        var payable = Assert.Single(result.GlEntries, g => g.AccountCode == "2110");
        Assert.Equal(400m, payable.Debit);
        Assert.Equal(0m, payable.Credit);
        var stock = Assert.Single(result.GlEntries, g => g.AccountCode == "1310");
        Assert.Equal(0m, stock.Debit);
        Assert.Equal(400m, stock.Credit);
        Assert.Equal(result.TotalDebit, result.TotalCredit);

        // Kardex: the goods leave the warehouse (negative qty/amount) on the return voucher.
        var sle = Assert.Single(_stock.AddedLedger, e => e.QtyChange < 0);
        Assert.Equal(-4m, sle.QtyChange);
        Assert.Equal(-400m, sle.Amount);
        Assert.Equal(_warehouse.Id, sle.WarehouseId);
        Assert.Equal("PRET-2026-00001", sle.VoucherNo);

        // D3: the debit note amortizes the referenced bill.
        Assert.Equal(600m, original.OutstandingAmount);
    }

    [Fact]
    public async Task PostReturnAsync_FullReturnWithTax_ReversesInputTaxAndZeroesOutstanding()
    {
        var original = await PostOriginalBillAsync(taxAmount: 210m);
        Assert.Equal(1210m, original.OutstandingAmount);

        var result = await CreateService().PostReturnAsync(
            NewReturnRequest(original.Id, original.Lines.Single().Id, qty: 10m, taxAmount: 210m, updateStock: true));

        Assert.Equal(-1000m, result.Invoice.NetTotal);
        Assert.Equal(-210m, result.Invoice.TaxTotal);
        Assert.Equal(-1210m, result.Invoice.GrandTotal);

        // Dr Accounts Payable 1210 / Cr Stock 1000 / Cr Input Tax 210.
        Assert.Equal(3, result.GlEntries.Count);
        var payable = Assert.Single(result.GlEntries, g => g.AccountCode == "2110");
        Assert.Equal(1210m, payable.Debit);
        var stock = Assert.Single(result.GlEntries, g => g.AccountCode == "1310");
        Assert.Equal(1000m, stock.Credit);
        var tax = Assert.Single(result.GlEntries, g => g.AccountCode == "1130");
        Assert.Equal(210m, tax.Credit);
        Assert.Equal(result.TotalDebit, result.TotalCredit);

        Assert.Equal(0m, original.OutstandingAmount);
    }

    [Fact]
    public async Task PostReturnAsync_AmountOnlyReturn_CreditsAccruedLiabilityAndSkipsStock()
    {
        var original = await PostOriginalBillAsync();

        var result = await CreateService().PostReturnAsync(
            NewReturnRequest(original.Id, original.Lines.Single().Id, qty: 4m, updateStock: false));

        Assert.False(result.Invoice.UpdateStock);

        // No physical movement: the interim accrued liability is reopened, stock stays untouched.
        var received = Assert.Single(result.GlEntries, g => g.AccountCode == "2120");
        Assert.Equal(400m, received.Credit);
        Assert.DoesNotContain(result.GlEntries, g => g.AccountCode == "1310");
        Assert.DoesNotContain(_stock.AddedLedger, e => e.QtyChange < 0);

        Assert.Equal(600m, original.OutstandingAmount);
    }

    [Fact]
    public async Task PostReturnAsync_TwoReturns_GetGaplessPretVouchers()
    {
        var original = await PostOriginalBillAsync();
        var line = original.Lines.Single().Id;

        var first = await CreateService().PostReturnAsync(
            NewReturnRequest(original.Id, line, qty: 3m, updateStock: false));
        var second = await CreateService().PostReturnAsync(
            NewReturnRequest(original.Id, line, qty: 2m, updateStock: false));

        Assert.Equal("PRET-2026-00001", first.Invoice.VoucherNo);
        Assert.Equal("PRET-2026-00002", second.Invoice.VoucherNo);
        Assert.Equal(500m, original.OutstandingAmount);
    }

    // ------------------------------------------------------------------------- rejections (T2)

    [Fact]
    public async Task PostReturnAsync_AgainstMissingBill_FailsWithReturnAgainstRequired()
    {
        var ex = await Assert.ThrowsAsync<PurchaseValidationException>(() =>
            CreateService().PostReturnAsync(
                NewReturnRequest(Guid.NewGuid(), Guid.NewGuid(), qty: 1m)));

        Assert.Equal(PurchaseErrorCodes.ReturnAgainstRequired, ex.Code);
    }

    [Fact]
    public async Task PostReturnAsync_AgainstAnotherCompanysBill_FailsWithReturnAgainstRequired()
    {
        var foreign = new PurchaseInvoice
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            SupplierId = _supplier.Id,
            Status = PurchaseInvoiceStatus.Unpaid,
            VoucherNo = "PINV-2026-00009",
            GrandTotal = 100m,
            OutstandingAmount = 100m,
            Lines = new List<PurchaseInvoiceLine>
            {
                new() { Id = Guid.NewGuid(), ItemId = _item.Id, Qty = 1m, Rate = 100m, Amount = 100m, LineNumber = 1 },
            },
        };
        _purchases.SeedInvoice(foreign);

        var ex = await Assert.ThrowsAsync<PurchaseValidationException>(() =>
            CreateService().PostReturnAsync(
                NewReturnRequest(foreign.Id, foreign.Lines.Single().Id, qty: 1m)));

        Assert.Equal(PurchaseErrorCodes.ReturnAgainstRequired, ex.Code);
    }

    [Fact]
    public async Task PostReturnAsync_AgainstAReturn_FailsWithReturnAgainstInvalid()
    {
        var previous = new PurchaseInvoice
        {
            Id = Guid.NewGuid(),
            CompanyId = _companyId,
            SupplierId = _supplier.Id,
            Status = PurchaseInvoiceStatus.Unpaid,
            VoucherNo = "PRET-2026-00001",
            IsReturn = true,
            GrandTotal = -100m,
            OutstandingAmount = -100m,
            Lines = new List<PurchaseInvoiceLine>
            {
                new() { Id = Guid.NewGuid(), ItemId = _item.Id, Qty = -1m, Rate = 100m, Amount = -100m, LineNumber = 1 },
            },
        };
        _purchases.SeedInvoice(previous);

        var ex = await Assert.ThrowsAsync<PurchaseValidationException>(() =>
            CreateService().PostReturnAsync(
                NewReturnRequest(previous.Id, previous.Lines.Single().Id, qty: 1m)));

        Assert.Equal(PurchaseErrorCodes.ReturnAgainstInvalid, ex.Code);
    }

    [Fact]
    public async Task PostReturnAsync_UnknownInvoiceLine_FailsWithReturnAgainstInvalid()
    {
        var original = await PostOriginalBillAsync();

        var ex = await Assert.ThrowsAsync<PurchaseValidationException>(() =>
            CreateService().PostReturnAsync(
                NewReturnRequest(original.Id, Guid.NewGuid(), qty: 1m)));

        Assert.Equal(PurchaseErrorCodes.ReturnAgainstInvalid, ex.Code);
    }

    [Fact]
    public async Task PostReturnAsync_NonPositiveQuantity_FailsWithReturnLineInvalid()
    {
        var original = await PostOriginalBillAsync();

        var ex = await Assert.ThrowsAsync<PurchaseValidationException>(() =>
            CreateService().PostReturnAsync(
                NewReturnRequest(original.Id, original.Lines.Single().Id, qty: 0m)));

        Assert.Equal(PurchaseErrorCodes.ReturnLineInvalid, ex.Code);
    }

    [Fact]
    public async Task PostReturnAsync_ExceedingOriginalLineQuantity_FailsWithReturnAmountExceeded()
    {
        var original = await PostOriginalBillAsync();

        var ex = await Assert.ThrowsAsync<PurchaseValidationException>(() =>
            CreateService().PostReturnAsync(
                NewReturnRequest(original.Id, original.Lines.Single().Id, qty: 11m)));

        Assert.Equal(PurchaseErrorCodes.ReturnAmountExceeded, ex.Code);
        Assert.Equal(1000m, original.OutstandingAmount);
    }

    [Fact]
    public async Task PostReturnAsync_CumulativeReturnsExceedingBill_FailWithReturnAmountExceeded()
    {
        var original = await PostOriginalBillAsync();
        var line = original.Lines.Single().Id;

        await CreateService().PostReturnAsync(NewReturnRequest(original.Id, line, qty: 6m, updateStock: false));

        var ex = await Assert.ThrowsAsync<PurchaseValidationException>(() =>
            CreateService().PostReturnAsync(NewReturnRequest(original.Id, line, qty: 6m, updateStock: false)));

        Assert.Equal(PurchaseErrorCodes.ReturnAmountExceeded, ex.Code);
        Assert.Equal(400m, original.OutstandingAmount);
    }

    // --------------------------------------------------------------- cancel guard (RET-G4 open)

    [Fact]
    public async Task Cancel_PurchaseReturn_IsRefusedWithoutTouchingTheLedger()
    {
        var original = await PostOriginalBillAsync();
        var posted = await CreateService().PostReturnAsync(
            NewReturnRequest(original.Id, original.Lines.Single().Id, qty: 4m, updateStock: true));

        var returnInvoice = _purchases.Invoices.Single(i => i.IsReturn);
        var glBefore = _stock.AddedGlEntries.Count;
        var ledgerBefore = _stock.AddedLedger.Count;

        var handler = new CancelPurchaseInvoiceCommandHandler(_companies, _purchases, _stock, _items);
        var result = await handler.HandleAsync(new CancelPurchaseInvoiceCommand(_companyId, returnInvoice.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.ReturnCancellationUnsupported, result.Error!.Code);
        Assert.Equal(PurchaseInvoiceStatus.Unpaid, returnInvoice.Status);
        Assert.Equal(-400m, returnInvoice.OutstandingAmount);
        Assert.Equal(glBefore, _stock.AddedGlEntries.Count);
        Assert.Equal(ledgerBefore, _stock.AddedLedger.Count);
        Assert.Equal(posted.Invoice.Id, returnInvoice.Id);
    }
}
