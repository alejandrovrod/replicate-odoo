using Erp.Application.DTOs;
using Erp.Application.Features.Selling.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Module 17-sales-taxes-discounts: global discount on Net Total (ERPNext default), server-side
/// tax recomputation over the discounted base, and the liability-only tax account gate -
/// exercised through <c>CreateSalesInvoiceCommandHandler</c> plus the tax legs of
/// <c>SubmitSalesInvoiceCommandHandler</c> (balanced GL asserted on the captured lines).
/// </summary>
public sealed class SalesInvoiceTaxesDiscountsTests
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

    private readonly Customer _customer;
    private readonly Item _item;
    private readonly Account _taxAccount;
    private readonly Account _receivable;
    private readonly Account _income;

    public SalesInvoiceTaxesDiscountsTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = Guid.NewGuid(),
            Name = "Acme Industrial",
            DefaultReceivableAccountCode = "1120",
            DefaultIncomeAccountCode = "4110",
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

        _taxAccount = Leaf("2430", "VAT Payable", AccountRootType.Liability);
        _receivable = Leaf("1120", "Accounts Receivable", AccountRootType.Asset, AccountType.Receivable);
        _income = Leaf("4110", "Sales Revenue", AccountRootType.Income);
        _accounts.Seed(_taxAccount, _receivable, _income);
        _accounts.AccountsByCodeMap["1120"] = new[] { _receivable };
        _accounts.AccountsByCodeMap["4110"] = new[] { _income };
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
        new(_salesInvoices, _customers, _companies, _accounts, _exchangeRates, new FakeStockRepository(), _warehouses);

    private CreateSalesInvoiceCommand NewInvoice(
        decimal discountPercentage = 0m,
        decimal discountAmount = 0m,
        IReadOnlyList<SalesInvoiceTaxCommandDto>? taxes = null,
        decimal quantity = 10m,
        decimal rate = 100m) =>
        new(_companyId, _customer.Id, PostingDate,
            new[] { new SalesInvoiceLineCommandDto(_item.Id, quantity, rate) },
            DiscountPercentage: discountPercentage,
            DiscountAmount: discountAmount,
            Taxes: taxes);

    // ------------------------------------------------------------------ create (draft)

    [Fact]
    public async Task Create_WithoutDiscountOrTaxes_KeepsLegacyTotals()
    {
        var result = await CreateHandler().HandleAsync(NewInvoice());

        Assert.True(result.IsSuccess);
        Assert.Equal(1000m, result.Value!.NetTotal);
        Assert.Equal(0m, result.Value.TaxTotal);
        Assert.Equal(1000m, result.Value.GrandTotal);
        Assert.Equal(0m, result.Value.DiscountAmount);
        Assert.Empty(result.Value.Taxes!);
    }

    [Fact]
    public async Task Create_WithDiscountPercentage_DerivesAmountAndGrandTotal()
    {
        var result = await CreateHandler().HandleAsync(NewInvoice(discountPercentage: 10m));

        Assert.True(result.IsSuccess);
        Assert.Equal(1000m, result.Value!.NetTotal);
        Assert.Equal(10m, result.Value.DiscountPercentage);
        Assert.Equal(100m, result.Value.DiscountAmount);
        Assert.Equal(900m, result.Value.GrandTotal);
    }

    [Fact]
    public async Task Create_WithMismatchedDiscountAmountAndPercentage_FailsWithInvalidDiscount()
    {
        var result = await CreateHandler().HandleAsync(NewInvoice(discountPercentage: 10m, discountAmount: 50m));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.InvalidDiscount, result.Error!.Code);
        Assert.Empty(_salesInvoices.Invoices);
    }

    [Fact]
    public async Task Create_WithDiscountPercentageOver100_FailsWithInvalidDiscount()
    {
        var result = await CreateHandler().HandleAsync(NewInvoice(discountPercentage: 150m));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.InvalidDiscount, result.Error!.Code);
    }

    [Fact]
    public async Task Create_WithTax_ComputesOnDiscountedBase()
    {
        // Net 1000 - 10% discount = 900 base; 21% VAT = 189; Grand = 1089 (spec §3 equation).
        var result = await CreateHandler().HandleAsync(NewInvoice(
            discountPercentage: 10m,
            taxes: new[] { new SalesInvoiceTaxCommandDto(_taxAccount.Id, 21m) }));

        Assert.True(result.IsSuccess);
        Assert.Equal(900m, result.Value!.GrandTotal - result.Value.TaxTotal);
        Assert.Equal(189m, result.Value.TaxTotal);
        Assert.Equal(1089m, result.Value.GrandTotal);

        var tax = Assert.Single(result.Value.Taxes!);
        Assert.Equal(_taxAccount.Id, tax.AccountId);
        Assert.Equal(21m, tax.Rate);
        Assert.Equal(189m, tax.TaxAmount);
    }

    [Fact]
    public async Task Create_WithIncomeTaxAccount_FailsWithInvalidTaxAccount()
    {
        var result = await CreateHandler().HandleAsync(NewInvoice(
            taxes: new[] { new SalesInvoiceTaxCommandDto(_income.Id, 21m) }));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.InvalidTaxAccount, result.Error!.Code);
    }

    [Fact]
    public async Task Create_WithUnknownTaxAccount_FailsWithInvalidTaxAccount()
    {
        var result = await CreateHandler().HandleAsync(NewInvoice(
            taxes: new[] { new SalesInvoiceTaxCommandDto(Guid.NewGuid(), 21m) }));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.InvalidTaxAccount, result.Error!.Code);
    }

    [Fact]
    public async Task Create_WithTaxRateOver100_FailsWithInvalidTaxRate()
    {
        var result = await CreateHandler().HandleAsync(NewInvoice(
            taxes: new[] { new SalesInvoiceTaxCommandDto(_taxAccount.Id, 150m) }));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.InvalidTaxRate, result.Error!.Code);
    }

    // ------------------------------------------------------------------ submit (posting)

    [Fact]
    public async Task Submit_WithTax_BooksBalancedReceivableRevenueAndTaxLegs()
    {
        var created = await CreateHandler().HandleAsync(NewInvoice(
            taxes: new[] { new SalesInvoiceTaxCommandDto(_taxAccount.Id, 21m) }));
        Assert.True(created.IsSuccess);

        var result = await SubmitHandler().HandleAsync(
            new SubmitSalesInvoiceCommand(_companyId, created.Value!.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(SalesInvoiceStatus.Unpaid, result.Value!.Status);
        Assert.Equal(1210m, result.Value.GrandTotal);

        Assert.Equal(3, _salesInvoices.GlEntries.Count);
        var ar = Assert.Single(_salesInvoices.GlEntries, l => l.AccountId == _receivable.Id);
        Assert.Equal(1210m, ar.Debit);
        Assert.Equal(0m, ar.Credit);
        var revenue = Assert.Single(_salesInvoices.GlEntries, l => l.AccountId == _income.Id);
        Assert.Equal(0m, revenue.Debit);
        Assert.Equal(1000m, revenue.Credit);
        var tax = Assert.Single(_salesInvoices.GlEntries, l => l.AccountId == _taxAccount.Id);
        Assert.Equal(0m, tax.Debit);
        Assert.Equal(210m, tax.Credit);

        Assert.Equal(
            _salesInvoices.GlEntries.Sum(l => l.Debit),
            _salesInvoices.GlEntries.Sum(l => l.Credit));
    }

    [Fact]
    public async Task Submit_WithDeactivatedTaxAccount_FailsWithInvalidTaxAccount()
    {
        var invoice = new SalesInvoice
        {
            Id = Guid.NewGuid(),
            CompanyId = _companyId,
            InvoiceNumber = "SINV-2026-00099",
            CustomerId = _customer.Id,
            Customer = _customer,
            PostingDate = PostingDate,
            DueDate = PostingDate,
            Status = SalesInvoiceStatus.Draft,
            NetTotal = 1000m,
            TaxTotal = 210m,
            GrandTotal = 1210m,
            OutstandingAmount = 1210m,
            CreatedAt = DateTimeOffset.UtcNow,
            Items = new List<SalesInvoiceItem>
            {
                new() { Id = Guid.NewGuid(), ItemId = _item.Id, Quantity = 10m, Rate = 100m, Amount = 1000m },
            },
            Taxes = new List<SalesInvoiceTax>
            {
                new() { Id = Guid.NewGuid(), AccountId = _taxAccount.Id, Rate = 21m, TaxAmount = 210m },
            },
        };
        _salesInvoices.Seed(invoice);
        _taxAccount.IsActive = false;

        var result = await SubmitHandler().HandleAsync(
            new SubmitSalesInvoiceCommand(_companyId, invoice.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.InvalidTaxAccount, result.Error!.Code);
        Assert.Empty(_salesInvoices.GlEntries);
    }
}
