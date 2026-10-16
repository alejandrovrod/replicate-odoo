using Erp.Application.Features.Selling.Commands;
using Erp.Application.Features.Tax.Commands;
using Erp.Application.Services;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Tax engine Fase 2 (spec tax-engine, TAX-G1..TAX-G4): deterministic Add/Deduct/cascade
/// computation, retention postings with exact GL balance, template expansion and the
/// account-required rejection - through the engine, the sales create/submit handlers and
/// the buying posting service.
/// </summary>
public sealed class TaxEngineTests
{
    private readonly TaxEngineService _engine = new();

    // ------------------------------------------------------------------ engine (TAX-G1/G3)

    [Fact]
    public void Calculate_HonorariosTemplate_MatchesTaxG1()
    {
        var result = _engine.Calculate(
            10000m,
            [
                new TaxRowInput(16m, TaxChargeType.Add, TaxCalculationBasis.OnNetTotal),
                new TaxRowInput(10m, TaxChargeType.Deduct, TaxCalculationBasis.OnNetTotal),
                new TaxRowInput(10.667m, TaxChargeType.Deduct, TaxCalculationBasis.OnNetTotal),
            ],
            decimals: 2);

        Assert.Equal(3, result.Rows.Count);
        Assert.Equal(1600m, result.Rows[0].TaxAmount); // IVA trasladado
        Assert.Equal(-1000m, result.Rows[1].TaxAmount); // Retención ISR
        Assert.Equal(-1066.70m, result.Rows[2].TaxAmount); // Retención IVA
        Assert.Equal(2066.70m, result.DeductTotal);
        Assert.Equal(9533.30m, result.GrandTotal); // 10000 + 1600 - 1000 - 1066.70
    }

    [Fact]
    public void Calculate_CascadeOnPreviousRow_MatchesTaxG3()
    {
        var result = _engine.Calculate(
            1000m,
            [
                new TaxRowInput(8m, TaxChargeType.Add, TaxCalculationBasis.OnNetTotal),
                new TaxRowInput(16m, TaxChargeType.Add, TaxCalculationBasis.OnPreviousRow),
            ],
            decimals: 2);

        Assert.Equal(80m, result.Rows[0].TaxAmount); // IEPS on net
        Assert.Equal(1000m, result.Rows[0].BaseAmount);
        Assert.Equal(1080m, result.Rows[1].BaseAmount); // net + IEPS
        Assert.Equal(172.80m, result.Rows[1].TaxAmount); // IVA on (Net + IEPS)
        Assert.Equal(252.80m, result.TaxTotal);
        Assert.Equal(1252.80m, result.GrandTotal);
    }

    [Fact]
    public void Calculate_FreightWithRetentionAndFixedRow()
    {
        var result = _engine.Calculate(
            5000m,
            [
                new TaxRowInput(16m, TaxChargeType.Add, TaxCalculationBasis.OnNetTotal),
                new TaxRowInput(10m, TaxChargeType.Deduct, TaxCalculationBasis.OnNetTotal),
                new TaxRowInput(0m, TaxChargeType.Deduct, TaxCalculationBasis.FixedAmount, FixedAmount: 250m),
            ],
            decimals: 2);

        Assert.Equal(800m, result.Rows[0].TaxAmount);
        Assert.Equal(-500m, result.Rows[1].TaxAmount);
        Assert.Equal(-250m, result.Rows[2].TaxAmount);
        Assert.Equal(750m, result.DeductTotal);
        Assert.Equal(5050m, result.GrandTotal); // 5000 + 800 - 500 - 250
    }

    [Fact]
    public void Calculate_ReturnBase_MirrorsSigns()
    {
        var result = _engine.Calculate(
            -10000m,
            [
                new TaxRowInput(16m, TaxChargeType.Add, TaxCalculationBasis.OnNetTotal),
                new TaxRowInput(10m, TaxChargeType.Deduct, TaxCalculationBasis.OnNetTotal),
            ],
            decimals: 2);

        Assert.Equal(-1600m, result.Rows[0].TaxAmount);
        Assert.Equal(1000m, result.Rows[1].TaxAmount);
        Assert.Equal(-600m, result.TaxTotal);
        Assert.Equal(-10600m, result.GrandTotal); // -10000 - 1600 + 1000
    }

    [Fact]
    public void Calculate_UnknownBasis_ThrowsWithCode()
    {
        var ex = Assert.Throws<TaxValidationException>(() => _engine.Calculate(
            1000m,
            [new TaxRowInput(16m, TaxChargeType.Add, (TaxCalculationBasis)99)],
            decimals: 2));
        Assert.Equal(TaxErrorCodes.InvalidTaxCalculationBasis, ex.Code);
    }

    [Fact]
    public void Calculate_RateOutOfRange_ThrowsWithCode()
    {
        var ex = Assert.Throws<TaxValidationException>(() => _engine.Calculate(
            1000m,
            [new TaxRowInput(101m)],
            decimals: 2));
        Assert.Equal(TaxErrorCodes.InvalidTaxRate, ex.Code);
    }

    // ------------------------------------------------------------- template CRUD (TAX-G4)

    [Fact]
    public async Task CreateTemplate_LineWithoutAccount_RejectedWithTaxAccountRequired()
    {
        var companies = new FakeCompanyRepository();
        var companyId = Guid.NewGuid();
        companies.Company = new Company { Id = companyId, TenantId = Guid.NewGuid(), Name = "Acme" };
        var handler = new CreateTaxTemplateCommandHandler(
            new FakeTaxTemplateRepository(), new FakeAccountRepository(), companies);

        var result = await handler.HandleAsync(new CreateTaxTemplateCommand(
            companyId,
            "Sin cuenta",
            "Sales",
            Lines: [new TaxTemplateLineInput(Guid.Empty, "IVA", 16m)]));

        Assert.False(result.IsSuccess);
        Assert.Equal(TaxErrorCodes.TaxAccountRequired, result.Error!.Code);
    }

    // ------------------------------------------------------- sales create+submit (TAX-G1/G2)

    private sealed class SalesFixture
    {
        public Guid CompanyId { get; } = Guid.NewGuid();
        public FakeSalesInvoiceRepository Invoices { get; } = new();
        public FakeCustomerRepository Customers { get; } = new();
        public FakeItemRepository Items { get; } = new();
        public FakeCompanyRepository Companies { get; } = new();
        public FakeAccountRepository Accounts { get; } = new();
        public FakeWarehouseRepository Warehouses { get; } = new();
        public FakeExchangeRateRepository ExchangeRates { get; } = new();
        public FakeStockRepository Stock { get; } = new();
        public FakeTaxTemplateRepository Templates { get; } = new();

        public Customer Customer { get; }
        public Item Item { get; }
        public Account Receivable { get; }
        public Account Revenue { get; }
        public Account VatPayable { get; }
        public Account IsrRetention { get; }
        public Account IvaRetention { get; }

        public SalesFixture()
        {
            Companies.Company = new Company
            {
                Id = CompanyId,
                TenantId = Guid.NewGuid(),
                Name = "Acme",
                DefaultReceivableAccountCode = "1105",
                DefaultIncomeAccountCode = "4101",
            };

            Customer = new Customer
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                CustomerCode = "CUST-001",
                CustomerName = "Cliente SA",
                IsActive = true,
            };
            Customers.Seed(Customer);

            Item = new Item
            {
                Id = Guid.NewGuid(),
                ItemCode = "SRV-001",
                ItemName = "Honorarios",
                ValuationMethod = ValuationMethod.Fifo,
                StockUomId = Guid.NewGuid(),
            };
            Items.Seed(Item);

            Receivable = Leaf("1105", "Clientes", AccountRootType.Asset);
            Revenue = Leaf("4101", "Ventas", AccountRootType.Income);
            VatPayable = Leaf("2108", "IVA Trasladado", AccountRootType.Liability);
            IsrRetention = Leaf("2109", "Retención ISR", AccountRootType.Liability);
            IvaRetention = Leaf("2110", "Retención IVA", AccountRootType.Liability);
            Accounts.Seed(Receivable, Revenue, VatPayable, IsrRetention, IvaRetention);
            Accounts.AccountsByCodeMap["1105"] = new[] { Receivable };
            Accounts.AccountsByCodeMap["4101"] = new[] { Revenue };
        }

        private Account Leaf(string code, string name, AccountRootType root) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = CompanyId,
            AccountCode = code,
            AccountName = name,
            RootType = root,
            IsGroup = false,
            IsActive = true,
        };

        public CreateSalesInvoiceCommandHandler CreateHandler() =>
            new(Invoices, Customers, Items, Companies, Accounts, Warehouses, new TaxEngineService(), Templates);

        public SubmitSalesInvoiceCommandHandler SubmitHandler() =>
            new(Invoices, Customers, Companies, Accounts, ExchangeRates, Stock, Warehouses);
    }

    private static SalesInvoiceTaxCommandDto Row(Guid accountId, decimal rate, TaxChargeType charge) =>
        new(accountId, rate, charge, TaxCalculationBasis.OnNetTotal);

    [Fact]
    public async Task CreateSalesInvoice_WithRetentions_ComputesSignedTotals()
    {
        var f = new SalesFixture();
        var result = await f.CreateHandler().HandleAsync(new CreateSalesInvoiceCommand(
            f.CompanyId,
            f.Customer.Id,
            new DateOnly(2026, 5, 4),
            [new SalesInvoiceLineCommandDto(f.Item.Id, 100m, 100m)],
            Taxes:
            [
                Row(f.VatPayable.Id, 16m, TaxChargeType.Add),
                Row(f.IsrRetention.Id, 10m, TaxChargeType.Deduct),
                Row(f.IvaRetention.Id, 10.667m, TaxChargeType.Deduct),
            ]));

        Assert.True(result.IsSuccess);
        Assert.Equal(10000m, result.Value!.NetTotal);
        Assert.Equal(-466.70m, result.Value.TaxTotal);
        Assert.Equal(9533.30m, result.Value.GrandTotal);
        Assert.Equal(3, result.Value.Taxes!.Count);
        Assert.Equal(-1000m, result.Value.Taxes.Single(t => t.ChargeType == TaxChargeType.Deduct && t.Rate == 10m).TaxAmount);
    }

    [Fact]
    public async Task SubmitSalesInvoice_WithRetentions_PostsExactTaxG2Ledger()
    {
        var f = new SalesFixture();
        var created = await f.CreateHandler().HandleAsync(new CreateSalesInvoiceCommand(
            f.CompanyId,
            f.Customer.Id,
            new DateOnly(2026, 5, 4),
            [new SalesInvoiceLineCommandDto(f.Item.Id, 100m, 100m)],
            Taxes:
            [
                Row(f.VatPayable.Id, 16m, TaxChargeType.Add),
                Row(f.IsrRetention.Id, 10m, TaxChargeType.Deduct),
                Row(f.IvaRetention.Id, 10.667m, TaxChargeType.Deduct),
            ]));
        Assert.True(created.IsSuccess);

        var submitted = await f.SubmitHandler().HandleAsync(
            new SubmitSalesInvoiceCommand(f.CompanyId, created.Value!.Id));
        Assert.True(submitted.IsSuccess);

        // TAX-G2 exact table (5 lines, strictly balanced).
        var gl = f.Invoices.GlEntries;
        Assert.Equal(5, gl.Count);
        Assert.Equal(gl.Sum(g => g.Debit), gl.Sum(g => g.Credit));

        var byCode = gl
            .Join(
                new[] { f.Receivable, f.Revenue, f.VatPayable, f.IsrRetention, f.IvaRetention },
                g => g.AccountId,
                a => a.Id,
                (g, a) => (Code: a.AccountCode, g.Debit, g.Credit))
            .ToList();

        Assert.Equal(5, byCode.Count); // every line hits a known account
        Assert.Equal(9533.30m, byCode.Single(x => x.Code == "1105").Debit);
        Assert.Equal(1000m, byCode.Single(x => x.Code == "2109").Debit);
        Assert.Equal(1066.70m, byCode.Single(x => x.Code == "2110").Debit);
        Assert.Equal(10000m, byCode.Single(x => x.Code == "4101").Credit);
        Assert.Equal(1600m, byCode.Single(x => x.Code == "2108").Credit);
        Assert.Equal(0m, byCode.Single(x => x.Code == "1105").Credit);
        Assert.Equal(0m, byCode.Single(x => x.Code == "4101").Debit);
    }

    [Fact]
    public async Task CreateSalesInvoice_FromTemplate_ExpandsRows()
    {
        var f = new SalesFixture();
        var template = new TaxTemplate
        {
            Id = Guid.NewGuid(),
            CompanyId = f.CompanyId,
            Title = "Honorarios PM a PF",
            Category = "Sales",
            IsDefault = true,
            Taxes =
            [
                new TaxTemplateItem { Id = Guid.NewGuid(), AccountId = f.VatPayable.Id, Description = "IVA", Rate = 16m },
                new TaxTemplateItem { Id = Guid.NewGuid(), AccountId = f.IsrRetention.Id, Description = "ISR", Rate = 10m, ChargeType = TaxChargeType.Deduct },
                new TaxTemplateItem { Id = Guid.NewGuid(), AccountId = f.IvaRetention.Id, Description = "IVA ret", Rate = 10.667m, ChargeType = TaxChargeType.Deduct },
            ],
        };
        f.Templates.Seed(template);

        var result = await f.CreateHandler().HandleAsync(new CreateSalesInvoiceCommand(
            f.CompanyId,
            f.Customer.Id,
            new DateOnly(2026, 5, 4),
            [new SalesInvoiceLineCommandDto(f.Item.Id, 100m, 100m)],
            TaxTemplateId: template.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(9533.30m, result.Value!.GrandTotal);
        Assert.Equal(3, result.Value.Taxes!.Count);
    }

    // ------------------------------------------------- purchase posting with retention

    private sealed class PurchaseFixture
    {
        public Guid CompanyId { get; } = Guid.NewGuid();
        public FakeCompanyRepository Companies { get; } = new();
        public FakeAccountRepository Accounts { get; } = new();
        public FakeWarehouseRepository Warehouses { get; } = new();
        public FakeItemRepository Items { get; } = new();
        public FakeStockRepository Stock { get; } = new();
        public FakePurchaseRepository Purchases { get; } = new();
        public FakeExchangeRateRepository ExchangeRates { get; } = new();
        public FakeAssetsRepository Assets { get; } = new();

        public Warehouse Warehouse { get; }
        public Item Item { get; }
        public Supplier Supplier { get; }
        public Account InputTax { get; }
        public Account Withholding { get; }

        public PurchaseFixture()
        {
            var stock = Leaf("1310", "Stock In Hand", AccountRootType.Asset);
            var received = Leaf("2120", "Stock Received But Not Billed", AccountRootType.Liability);
            var payable = Leaf("2110", "Accounts Payable", AccountRootType.Liability);
            InputTax = Leaf("1130", "Input Tax Recoverable", AccountRootType.Asset);
            Withholding = Leaf("2140", "ISR Withheld Payable", AccountRootType.Liability);
            var diff = Leaf("5120", "Price Difference", AccountRootType.Expense);
            Accounts.Seed(stock, received, payable, InputTax, Withholding, diff);
            Accounts.AccountsByCodeMap["2120"] = new[] { received };
            Accounts.AccountsByCodeMap["2110"] = new[] { payable };
            Accounts.AccountsByCodeMap["1130"] = new[] { InputTax };
            Accounts.AccountsByCodeMap["5120"] = new[] { diff };

            Companies.Company = new Company
            {
                Id = CompanyId,
                TenantId = Guid.NewGuid(),
                Name = "Acme",
                StockReceivedAccountCode = "2120",
                AccountsPayableAccountCode = "2110",
                InputTaxRecoverableAccountCode = "1130",
                PriceDifferenceAccountCode = "5120",
            };

            Warehouse = new Warehouse
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                WarehouseCode = "WH-01",
                AccountId = stock.Id,
            };
            Warehouses.Seed(Warehouse);

            Item = new Item
            {
                Id = Guid.NewGuid(),
                ItemCode = "IT-001",
                ItemName = "Steel Bracket",
                ValuationMethod = ValuationMethod.Fifo,
                StockUomId = Guid.NewGuid(),
            };
            Items.Seed(Item);

            Supplier = new Supplier
            {
                Id = Guid.NewGuid(),
                Code = "SUP-001",
                Name = "Supplier SA",
                IsActive = true,
            };
        }

        private Account Leaf(string code, string name, AccountRootType root) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = CompanyId,
            AccountCode = code,
            AccountName = name,
            RootType = root,
            IsGroup = false,
            IsActive = true,
        };

        public PurchasePostingService Service() =>
            new(Companies, Accounts, Warehouses, Items, Stock, Purchases, ExchangeRates, Assets);
    }

    [Fact]
    public async Task PostPurchaseInvoice_WithWithholding_BooksBalancedRetentionLegs()
    {
        var f = new PurchaseFixture();
        var postingDate = new DateOnly(2026, 3, 2);

        // Receipt first (10 @ 100): the three-way match demands billed receipt lines.
        await f.Service().PostReceiptAsync(new PurchaseReceiptPostingRequest(
            f.CompanyId, f.Warehouse.Id, f.Supplier.Id, null, postingDate,
            [new PurchaseReceiptPostingLine(f.Item.Id, 10m, 100m)]));
        var receiptLine = Assert.Single(f.Purchases.Receipts.Single().Lines);

        var result = await f.Service().PostInvoiceAsync(new PurchaseInvoicePostingRequest(
            f.CompanyId,
            f.Supplier.Id,
            "BILL-001",
            null,
            postingDate,
            postingDate.AddDays(30),
            0m,
            [new PurchaseInvoicePostingLine(receiptLine.Id, f.Item.Id, 10m, 100m)],
            Taxes:
            [
                new PurchaseInvoiceTaxInput(f.InputTax.Id, 16m, TaxChargeType.Add),
                new PurchaseInvoiceTaxInput(f.Withholding.Id, 10m, TaxChargeType.Deduct),
            ]));

        // Net 1000 + IVA 160 - ISR ret 100 = payable 1060.
        Assert.Equal(1000m, result.Invoice.NetTotal);
        Assert.Equal(60m, result.Invoice.TaxTotal);
        Assert.Equal(100m, result.Invoice.WithholdingTaxTotal);
        Assert.Equal(1060m, result.Invoice.GrandTotal);
        Assert.Equal(2, result.Invoice.Taxes!.Count);

        // Balanced legs: Dr interim 1000 + Dr input tax 160 / Cr withholding 100 + Cr payable 1060.
        Assert.Equal(result.TotalDebit, result.TotalCredit);
        Assert.Equal(4, result.GlEntries.Count);

        // GLEntryDto carries the account code (readable audit, Fase 2.1).
        var coded = result.GlEntries.ToDictionary(g => g.AccountCode);
        Assert.Equal(1000m, coded["2120"].Debit);
        Assert.Equal(160m, coded["1130"].Debit);
        Assert.Equal(100m, coded["2140"].Credit);
        Assert.Equal(1060m, coded["2110"].Credit);
        Assert.Equal(1160m, result.TotalDebit);
        Assert.Equal(2, result.Invoice.Taxes.Count(t => t.BaseAmount == 1000m));
    }
}
