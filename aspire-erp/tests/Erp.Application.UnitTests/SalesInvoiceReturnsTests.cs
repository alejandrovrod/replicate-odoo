using Erp.Application.Features.Selling.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Module 18-sales-returns: credit-note provenance (submitted original, same customer),
/// all-negative lines/totals (ERPNext <c>validate_qty</c> parity), the |GrandTotal| ceiling,
/// inverse GL sides with absolute magnitudes (Constitution IV.3), and stock re-entry -
/// exercised through the invoice create/submit handlers.
/// </summary>
public sealed class SalesInvoiceReturnsTests
{
    private static readonly DateOnly PostingDate = new(2026, 5, 4);

    private readonly Guid _companyId = Guid.NewGuid();

    private readonly FakeSalesInvoiceRepository _salesInvoices = new();
    private readonly FakeCustomerRepository _customers = new();
    private readonly FakeItemRepository _items = new();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeWarehouseRepository _warehouses = new();
    private readonly FakeExchangeRateRepository _exchangeRates = new();
    private readonly FakeStockRepository _stock = new();

    private readonly Customer _customer;
    private readonly Item _item;
    private readonly Account _receivable;
    private readonly Account _income;
    private readonly Account _taxAccount;
    private readonly Account _stockAccount;
    private readonly Account _cogsAccount;
    private readonly Warehouse _warehouse;

    public SalesInvoiceReturnsTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = Guid.NewGuid(),
            Name = "Acme Industrial",
            DefaultReceivableAccountCode = "1120",
            DefaultIncomeAccountCode = "4110",
            CogsAccountCode = "5120",
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

        _receivable = Leaf("1120", "Accounts Receivable", AccountRootType.Asset, AccountType.Receivable);
        _income = Leaf("4110", "Sales Revenue", AccountRootType.Income);
        _taxAccount = Leaf("2430", "VAT Payable", AccountRootType.Liability);
        _stockAccount = Leaf("1310", "Inventory", AccountRootType.Asset, AccountType.Stock);
        _cogsAccount = Leaf("5120", "Cost of Goods Sold", AccountRootType.Expense, AccountType.COGS);
        _accounts.Seed(_receivable, _income, _taxAccount, _stockAccount, _cogsAccount);
        _accounts.AccountsByCodeMap["1120"] = new[] { _receivable };
        _accounts.AccountsByCodeMap["4110"] = new[] { _income };
        _accounts.AccountsByCodeMap["5120"] = new[] { _cogsAccount };

        _warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            CompanyId = _companyId,
            WarehouseCode = "WH-01",
            AccountId = _stockAccount.Id,
        };
        _warehouses.Seed(_warehouse);
    }

    private Account Leaf(string code, string name, AccountRootType root, AccountType type = AccountType.Other) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        CompanyId = _companyId,
        AccountCode = code,
        AccountName = name,
        RootType = root,
        Type = type,
        IsGroup = false,
        IsActive = true,
    };

    private CreateSalesInvoiceCommandHandler CreateHandler() =>
        new(_salesInvoices, _customers, _items, _companies, _accounts, _warehouses);

    private SubmitSalesInvoiceCommandHandler SubmitHandler() =>
        new(_salesInvoices, _customers, _companies, _accounts, _exchangeRates, _stock, _warehouses);

    /// <summary>Submitted original of 10 x 100 owned by the test customer.</summary>
    private SalesInvoice SeedOriginal(SalesInvoiceStatus status = SalesInvoiceStatus.Unpaid, Guid? customerId = null)
    {
        var original = new SalesInvoice
        {
            Id = Guid.NewGuid(),
            CompanyId = _companyId,
            InvoiceNumber = "SINV-2026-00042",
            CustomerId = customerId ?? _customer.Id,
            Customer = _customer,
            PostingDate = PostingDate,
            DueDate = PostingDate,
            Status = status,
            NetTotal = 1000m,
            TaxTotal = 0m,
            GrandTotal = 1000m,
            OutstandingAmount = 1000m,
            CreatedAt = DateTimeOffset.UtcNow,
            Items = new List<SalesInvoiceItem>
            {
                new() { Id = Guid.NewGuid(), ItemId = _item.Id, Quantity = 10m, Rate = 100m, Amount = 1000m },
            },
        };
        _salesInvoices.Seed(original);
        return original;
    }

    private CreateSalesInvoiceCommand ReturnCommand(
        Guid returnAgainstId,
        decimal quantity = -4m,
        decimal rate = 100m,
        bool updateStock = false) =>
        new(_companyId, _customer.Id, PostingDate,
            new[] { new SalesInvoiceLineCommandDto(_item.Id, quantity, rate) },
            IsReturn: true,
            ReturnAgainstId: returnAgainstId,
            UpdateStock: updateStock,
            SourceWarehouseId: updateStock ? _warehouse.Id : null);

    // ------------------------------------------------------------------ create (draft)

    [Fact]
    public async Task Create_ReturnWithoutOriginal_FailsWithReturnAgainstRequired()
    {
        var result = await CreateHandler().HandleAsync(
            new(_companyId, _customer.Id, PostingDate,
                new[] { new SalesInvoiceLineCommandDto(_item.Id, -1m, 100m) },
                IsReturn: true));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.ReturnAgainstRequired, result.Error!.Code);
    }

    [Fact]
    public async Task Create_ReturnAgainstDraftOriginal_FailsWithReturnAgainstInvalid()
    {
        var draft = SeedOriginal(SalesInvoiceStatus.Draft);

        var result = await CreateHandler().HandleAsync(ReturnCommand(draft.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.ReturnAgainstInvalid, result.Error!.Code);
    }

    [Fact]
    public async Task Create_ReturnAgainstAnotherCustomersInvoice_FailsWithReturnAgainstInvalid()
    {
        var foreign = SeedOriginal(customerId: Guid.NewGuid());

        var result = await CreateHandler().HandleAsync(ReturnCommand(foreign.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.ReturnAgainstInvalid, result.Error!.Code);
    }

    [Fact]
    public async Task Create_ReturnWithPositiveLine_FailsWithInvalidQuantity()
    {
        var original = SeedOriginal();

        var result = await CreateHandler().HandleAsync(ReturnCommand(original.Id, quantity: 4m));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.InvalidQuantity, result.Error!.Code);
    }

    [Fact]
    public async Task Create_ReturnExceedingOriginal_FailsWithReturnAmountExceeded()
    {
        var original = SeedOriginal();

        var result = await CreateHandler().HandleAsync(ReturnCommand(original.Id, quantity: -20m));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.ReturnAmountExceeded, result.Error!.Code);
    }

    [Fact]
    public async Task Create_PartialReturn_PersistsNegativeTotals()
    {
        var original = SeedOriginal();

        var result = await CreateHandler().HandleAsync(ReturnCommand(original.Id));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsReturn);
        Assert.Equal(original.Id, result.Value.ReturnAgainstId);
        Assert.Equal(-400m, result.Value.NetTotal);
        Assert.Equal(0m, result.Value.TaxTotal);
        Assert.Equal(-400m, result.Value.GrandTotal);
        Assert.Equal(-400m, result.Value.OutstandingAmount);
    }

    // ------------------------------------------------------------------ submit (posting)

    [Fact]
    public async Task Submit_Return_BooksInverseSidesWithAbsoluteMagnitudes()
    {
        var original = SeedOriginal();
        var created = await CreateHandler().HandleAsync(ReturnCommand(original.Id));
        Assert.True(created.IsSuccess);

        var result = await SubmitHandler().HandleAsync(
            new SubmitSalesInvoiceCommand(_companyId, created.Value!.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(SalesInvoiceStatus.Unpaid, result.Value!.Status);

        // No negative money ever touches the ledger (Constitution IV.3): the same two
        // accounts swap sides with |amounts|.
        Assert.Equal(2, _salesInvoices.GlEntries.Count);
        var ar = Assert.Single(_salesInvoices.GlEntries, l => l.AccountId == _receivable.Id);
        Assert.Equal(0m, ar.Debit);
        Assert.Equal(400m, ar.Credit);
        var revenue = Assert.Single(_salesInvoices.GlEntries, l => l.AccountId == _income.Id);
        Assert.Equal(400m, revenue.Debit);
        Assert.Equal(0m, revenue.Credit);

        Assert.Equal(
            _salesInvoices.GlEntries.Sum(l => l.Debit),
            _salesInvoices.GlEntries.Sum(l => l.Credit));

        // The customer's debt shrinks by the credit amount.
        Assert.Equal(-400m, _customer.OutstandingAmount);
    }

    [Fact]
    public async Task Submit_ReturnWithTax_ReversesTaxLegAsDebit()
    {
        // Full return of a taxed invoice: the ceiling compares GrandTotals (1210 vs 1210).
        var original = SeedOriginal();
        original.NetTotal = 1000m;
        original.TaxTotal = 210m;
        original.GrandTotal = 1210m;
        original.OutstandingAmount = 1210m;
        var created = await CreateHandler().HandleAsync(
            new(_companyId, _customer.Id, PostingDate,
                new[] { new SalesInvoiceLineCommandDto(_item.Id, -10m, 100m) },
                Taxes: new[] { new SalesInvoiceTaxCommandDto(_taxAccount.Id, 21m) },
                IsReturn: true,
                ReturnAgainstId: original.Id));
        Assert.True(created.IsSuccess);
        Assert.Equal(-1210m, created.Value!.GrandTotal);

        var result = await SubmitHandler().HandleAsync(
            new SubmitSalesInvoiceCommand(_companyId, created.Value.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, _salesInvoices.GlEntries.Count);

        var tax = Assert.Single(_salesInvoices.GlEntries, l => l.AccountId == _taxAccount.Id);
        Assert.Equal(210m, tax.Debit);
        Assert.Equal(0m, tax.Credit);

        Assert.Equal(
            _salesInvoices.GlEntries.Sum(l => l.Debit),
            _salesInvoices.GlEntries.Sum(l => l.Credit));
    }

    [Fact]
    public async Task Submit_ReturnWithUpdateStock_ReentersInventory()
    {
        var original = SeedOriginal();
        var created = await CreateHandler().HandleAsync(ReturnCommand(original.Id, updateStock: true));
        Assert.True(created.IsSuccess);

        var result = await SubmitHandler().HandleAsync(
            new SubmitSalesInvoiceCommand(_companyId, created.Value!.Id));

        Assert.True(result.IsSuccess);

        // Kardex: positive quantity change (goods come back).
        var sle = Assert.Single(_stock.AddedLedger);
        Assert.Equal(_item.Id, sle.ItemId);
        Assert.Equal(_warehouse.Id, sle.WarehouseId);
        Assert.Equal(4m, sle.QtyChange);

        // Ledger: Debit Inventory / Credit COGS (spec 18 §3).
        var stockLine = Assert.Single(_salesInvoices.GlEntries, l => l.AccountId == _stockAccount.Id);
        Assert.Equal(400m, stockLine.Debit);
        Assert.Equal(0m, stockLine.Credit);
        var cogsLine = Assert.Single(_salesInvoices.GlEntries, l => l.AccountId == _cogsAccount.Id);
        Assert.Equal(0m, cogsLine.Debit);
        Assert.Equal(400m, cogsLine.Credit);

        Assert.Equal(
            _salesInvoices.GlEntries.Sum(l => l.Debit),
            _salesInvoices.GlEntries.Sum(l => l.Credit));
    }
}
