using Erp.Application.Features.Selling.Commands;
using Erp.Application.Features.Selling.Queries;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Sales-invoice ERPNext parity (.specify/modules/sales-invoice-erpnext-parity, scenarios
/// SI-G1/SI-G2/SI-G3): Draft-time advance reservation, submit-time settlement against the
/// live payment balances, FIFO-valued direct stock issue, and the eligible-advances read -
/// exercised through the create/submit handlers plus the query handler.
/// </summary>
public sealed class SalesInvoiceAdvanceParityTests
{
    private static readonly DateOnly PostingDate = new(2026, 10, 15);

    private readonly Guid _companyId = Guid.NewGuid();

    private readonly FakeSalesInvoiceRepository _salesInvoices = new();
    private readonly FakeCustomerRepository _customers = new();
    private readonly FakeItemRepository _items = new();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeWarehouseRepository _warehouses = new();
    private readonly FakeExchangeRateRepository _exchangeRates = new();
    private readonly FakeStockRepository _stock = new();
    private readonly FakeBankRepository _banks = new();

    private readonly Customer _customer;
    private readonly Item _item;
    private readonly Warehouse _warehouse;
    private readonly Account _receivable;
    private readonly Account _income;
    private readonly Account _stockAccount;
    private readonly Account _cogsAccount;

    public SalesInvoiceAdvanceParityTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = Guid.NewGuid(),
            Name = "Acme Industrial",
            DefaultReceivableAccountCode = "1120",
            DefaultIncomeAccountCode = "4110",
            CogsAccountCode = "5210",
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

        _accounts.Seed(
            _receivable = Leaf("1120", "Accounts Receivable", AccountRootType.Asset, AccountType.Receivable),
            _income = Leaf("4110", "Sales Revenue", AccountRootType.Income),
            _stockAccount = Leaf("1310", "Inventory", AccountRootType.Asset, AccountType.Stock),
            _cogsAccount = Leaf("5210", "Cost of Goods Sold", AccountRootType.Expense, AccountType.COGS));
        _accounts.AccountsByCodeMap["1120"] = new[] { _receivable };
        _accounts.AccountsByCodeMap["4110"] = new[] { _income };
        _accounts.AccountsByCodeMap["5210"] = new[] { _cogsAccount };

        _warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            CompanyId = _companyId,
            WarehouseCode = "WH-MAIN",
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
        new(_salesInvoices, _customers, _items, _companies, _accounts, _warehouses, banks: _banks);

    private SubmitSalesInvoiceCommandHandler SubmitHandler() =>
        new(_salesInvoices, _customers, _companies, _accounts, _exchangeRates, _stock, _warehouses,
            items: _items, banks: _banks);

    private PaymentEntry NewAdvance(
        decimal paidAmount,
        decimal unallocatedAmount,
        Guid? customerId = null,
        PaymentDocumentStatus status = PaymentDocumentStatus.Submitted,
        PaymentType type = PaymentType.Receive,
        DateOnly? paymentDate = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            BankAccountId = Guid.NewGuid(),
            PaymentType = type,
            PartyType = PaymentPartyType.Customer,
            PartyId = customerId ?? _customer.Id,
            PaymentDate = paymentDate ?? PostingDate,
            PaidAmount = paidAmount,
            ReceivedAmount = paidAmount,
            UnallocatedAmount = unallocatedAmount,
            TotalAllocatedAmount = paidAmount - unallocatedAmount,
            DocumentStatus = status,
            VoucherNo = $"PAY-2026-{Guid.NewGuid():N}",
        };

    private CreateSalesInvoiceCommand NewInvoice(
        IReadOnlyList<SalesInvoiceAdvanceCommandDto>? advances = null,
        decimal quantity = 2m,
        decimal rate = 100m,
        bool updateStock = false) =>
        new(_companyId, _customer.Id, PostingDate,
            new[] { new SalesInvoiceLineCommandDto(_item.Id, quantity, rate) },
            UpdateStock: updateStock,
            SourceWarehouseId: updateStock ? _warehouse.Id : null,
            Advances: advances);

    // ------------------------------------------------------------------ create (draft)

    [Fact]
    public async Task Create_WithAdvance_ReservesSliceAndNetsOutstanding()
    {
        var payment = NewAdvance(paidAmount: 150m, unallocatedAmount: 150m);
        _banks.SeedPaymentEntry(payment);

        var result = await CreateHandler().HandleAsync(
            NewInvoice(new[] { new SalesInvoiceAdvanceCommandDto(payment.Id, 150m) }));

        Assert.True(result.IsSuccess);
        Assert.Equal(150m, result.Value!.TotalAdvance);
        Assert.Equal(50m, result.Value.OutstandingAmount);
        var saved = _salesInvoices.Invoices.Single(i => i.Id == result.Value.Id);
        var slice = Assert.Single(saved.Advances);
        Assert.Equal(payment.Id, slice.PaymentEntryId);
        Assert.Equal(150m, slice.AllocatedAmount);
        Assert.Equal("SalesInvoice", slice.ReferenceDocumentType);
    }

    [Fact]
    public async Task Create_AdvanceBeyondTotal_FailsWithAdvanceExceedsInvoiceTotal()
    {
        var payment = NewAdvance(paidAmount: 500m, unallocatedAmount: 500m);
        _banks.SeedPaymentEntry(payment);

        // SI-G3: $500 against a $200 invoice.
        var result = await CreateHandler().HandleAsync(
            NewInvoice(new[] { new SalesInvoiceAdvanceCommandDto(payment.Id, 500m) }));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.AdvanceExceedsInvoiceTotal, result.Error!.Code);
    }

    [Fact]
    public async Task Create_UnknownPayment_FailsWithPaymentEntryNotFoundOrDepleted()
    {
        var result = await CreateHandler().HandleAsync(
            NewInvoice(new[] { new SalesInvoiceAdvanceCommandDto(Guid.NewGuid(), 50m) }));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.PaymentEntryNotFoundOrDepleted, result.Error!.Code);
    }

    [Fact]
    public async Task Create_AmountBeyondUnallocated_FailsWithPaymentEntryNotFoundOrDepleted()
    {
        var payment = NewAdvance(paidAmount: 150m, unallocatedAmount: 100m);
        _banks.SeedPaymentEntry(payment);

        var result = await CreateHandler().HandleAsync(
            NewInvoice(new[] { new SalesInvoiceAdvanceCommandDto(payment.Id, 150m) }));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.PaymentEntryNotFoundOrDepleted, result.Error!.Code);
    }

    [Fact]
    public async Task Create_DraftPayment_IsNotEligible()
    {
        var payment = NewAdvance(
            paidAmount: 150m, unallocatedAmount: 150m, status: PaymentDocumentStatus.Draft);
        _banks.SeedPaymentEntry(payment);

        var result = await CreateHandler().HandleAsync(
            NewInvoice(new[] { new SalesInvoiceAdvanceCommandDto(payment.Id, 150m) }));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.PaymentEntryNotFoundOrDepleted, result.Error!.Code);
    }

    [Fact]
    public async Task Create_AdvanceOnReturn_Fails()
    {
        var payment = NewAdvance(paidAmount: 150m, unallocatedAmount: 150m);
        _banks.SeedPaymentEntry(payment);

        // An otherwise-valid credit note (submitted original): only the advance cross fails.
        var original = new SalesInvoice
        {
            Id = Guid.NewGuid(),
            CompanyId = _companyId,
            InvoiceNumber = "SINV-2026-00042",
            CustomerId = _customer.Id,
            PostingDate = PostingDate,
            DueDate = PostingDate,
            Status = SalesInvoiceStatus.Unpaid,
            NetTotal = 1000m,
            GrandTotal = 1000m,
            OutstandingAmount = 1000m,
            Items = new List<SalesInvoiceItem>
            {
                new() { Id = Guid.NewGuid(), ItemId = _item.Id, Quantity = 10m, Rate = 100m, Amount = 1000m },
            },
        };
        _salesInvoices.Seed(original);

        var result = await CreateHandler().HandleAsync(
            new(_companyId, _customer.Id, PostingDate,
                new[] { new SalesInvoiceLineCommandDto(_item.Id, -1m, 100m) },
                IsReturn: true,
                ReturnAgainstId: original.Id,
                Advances: new[] { new SalesInvoiceAdvanceCommandDto(payment.Id, 100m) }));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.ValidationFailed, result.Error!.Code);
    }

    // ------------------------------------------------------------------ submit (settle)

    [Fact]
    public async Task Submit_WithPartialAdvance_SettlesOutstandingAndConsumesBalance()
    {
        var payment = NewAdvance(paidAmount: 150m, unallocatedAmount: 150m);
        _banks.SeedPaymentEntry(payment);

        var created = await CreateHandler().HandleAsync(
            NewInvoice(new[] { new SalesInvoiceAdvanceCommandDto(payment.Id, 150m) }));
        Assert.True(created.IsSuccess);

        var submitted = await SubmitHandler().HandleAsync(
            new SubmitSalesInvoiceCommand(_companyId, created.Value!.Id));

        // SI-G2: $200 billed, $150 crossed -> $50 outstanding, PartiallyPaid.
        Assert.True(submitted.IsSuccess);
        Assert.Equal(50m, submitted.Value!.OutstandingAmount);
        Assert.Equal(150m, submitted.Value.PaidAmount);
        Assert.Equal(150m, submitted.Value.TotalAdvance);
        Assert.Equal(SalesInvoiceStatus.PartiallyPaid, submitted.Value.Status);
        Assert.Equal(0m, payment.UnallocatedAmount);
        Assert.Equal(150m, payment.TotalAllocatedAmount);
        // The customer only owes the net: the cross funded the rest at payment time.
        Assert.Equal(50m, _customer.OutstandingAmount);
    }

    [Fact]
    public async Task Submit_WithFullAdvance_MarksInvoicePaid()
    {
        var payment = NewAdvance(paidAmount: 200m, unallocatedAmount: 200m);
        _banks.SeedPaymentEntry(payment);

        var created = await CreateHandler().HandleAsync(
            NewInvoice(new[] { new SalesInvoiceAdvanceCommandDto(payment.Id, 200m) }));
        Assert.True(created.IsSuccess);

        var submitted = await SubmitHandler().HandleAsync(
            new SubmitSalesInvoiceCommand(_companyId, created.Value!.Id));

        Assert.True(submitted.IsSuccess);
        Assert.Equal(0m, submitted.Value!.OutstandingAmount);
        Assert.Equal(SalesInvoiceStatus.Paid, submitted.Value.Status);
        Assert.Equal(0m, _customer.OutstandingAmount);
    }

    [Fact]
    public async Task Submit_DepletedAdvanceBetweenDraftAndSubmit_Fails()
    {
        var payment = NewAdvance(paidAmount: 150m, unallocatedAmount: 150m);
        _banks.SeedPaymentEntry(payment);

        var created = await CreateHandler().HandleAsync(
            NewInvoice(new[] { new SalesInvoiceAdvanceCommandDto(payment.Id, 150m) }));
        Assert.True(created.IsSuccess);

        // A concurrent cross consumes the payment before this invoice submits.
        payment.UnallocatedAmount = 0m;

        var submitted = await SubmitHandler().HandleAsync(
            new SubmitSalesInvoiceCommand(_companyId, created.Value!.Id));

        Assert.False(submitted.IsSuccess);
        Assert.Equal(SellingErrorCodes.PaymentEntryNotFoundOrDepleted, submitted.Error!.Code);
    }

    // ------------------------------------------------------------------ submit (FIFO stock)

    [Fact]
    public async Task Submit_UpdateStock_IssuesAtFifoCostWithKardexRow()
    {
        // 10 units @ $50 in WH-MAIN: the FIFO layer the $100-rate bill consumes.
        _stock.SeedLedger(new StockLedgerEntry
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ItemId = _item.Id,
            WarehouseId = _warehouse.Id,
            VoucherType = "StockEntry",
            VoucherNo = "MR-2026-00001",
            PostingDate = PostingDate.AddDays(-5),
            QtyChange = 10m,
            ValuationRate = 50m,
            Amount = 500m,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        var created = await CreateHandler().HandleAsync(NewInvoice(updateStock: true));
        Assert.True(created.IsSuccess);

        var submitted = await SubmitHandler().HandleAsync(
            new SubmitSalesInvoiceCommand(_companyId, created.Value!.Id));

        Assert.True(submitted.IsSuccess);

        // SI-G1 money: revenue at the billed rate ($200), COGS at the FIFO cost ($100).
        var cogs = _salesInvoices.GlEntries.Single(e =>
            e.AccountId == _cogsAccount.Id && e.Debit > 0m);
        Assert.Equal(100m, cogs.Debit);
        var stock = _salesInvoices.GlEntries.Single(e =>
            e.AccountId == _stockAccount.Id && e.Credit > 0m);
        Assert.Equal(100m, stock.Credit);

        // SI-G1 Kardex: -2 units at the $50 layer rate.
        var issue = Assert.Single(_stock.AddedLedger);
        Assert.Equal(-2m, issue.QtyChange);
        Assert.Equal(50m, issue.ValuationRate);
        Assert.Equal(-100m, issue.Amount);

        // Overselling prevention: the Kardex range was locked before layers were read.
        Assert.NotEmpty(_stock.StockRangeLocks);
    }

    [Fact]
    public async Task Submit_UpdateStockWithoutLayers_FailsWithInsufficientStock()
    {
        var created = await CreateHandler().HandleAsync(NewInvoice(updateStock: true));
        Assert.True(created.IsSuccess);

        var submitted = await SubmitHandler().HandleAsync(
            new SubmitSalesInvoiceCommand(_companyId, created.Value!.Id));

        Assert.False(submitted.IsSuccess);
        Assert.Equal(SellingErrorCodes.InsufficientStockForInvoice, submitted.Error!.Code);
    }

    [Fact]
    public async Task Submit_UpdateStockWithoutWarehouse_FailsWithWarehouseRequired()
    {
        var invoice = new SalesInvoice
        {
            Id = Guid.NewGuid(),
            CompanyId = _companyId,
            InvoiceNumber = "SINV-2026-00099",
            CustomerId = _customer.Id,
            PostingDate = PostingDate,
            DueDate = PostingDate,
            Status = SalesInvoiceStatus.Draft,
            UpdateStock = true,
            SourceWarehouseId = null,
            NetTotal = 200m,
            GrandTotal = 200m,
            OutstandingAmount = 200m,
            Items = new List<SalesInvoiceItem>
            {
                new() { Id = Guid.NewGuid(), ItemId = _item.Id, Quantity = 2m, Rate = 100m, Amount = 200m },
            },
        };
        _salesInvoices.Seed(invoice);

        var submitted = await SubmitHandler().HandleAsync(
            new SubmitSalesInvoiceCommand(_companyId, invoice.Id));

        Assert.False(submitted.IsSuccess);
        Assert.Equal(SellingErrorCodes.UpdateStockWarehouseRequired, submitted.Error!.Code);
    }

    // ------------------------------------------------------------------ eligible advances

    [Fact]
    public async Task EligibleAdvances_ReturnsOnlySubmittedReceiptsWithBalance()
    {
        var eligible = NewAdvance(
            paidAmount: 150m, unallocatedAmount: 150m, paymentDate: PostingDate.AddDays(-2));
        var draft = NewAdvance(
            paidAmount: 90m, unallocatedAmount: 90m, status: PaymentDocumentStatus.Draft);
        var consumed = NewAdvance(paidAmount: 80m, unallocatedAmount: 0m);
        var foreign = NewAdvance(paidAmount: 70m, unallocatedAmount: 70m, customerId: Guid.NewGuid());
        var pay = NewAdvance(
            paidAmount: 60m, unallocatedAmount: 60m, type: PaymentType.Pay);
        _banks.SeedPaymentEntry(eligible);
        _banks.SeedPaymentEntry(draft);
        _banks.SeedPaymentEntry(consumed);
        _banks.SeedPaymentEntry(foreign);
        _banks.SeedPaymentEntry(pay);

        var handler = new GetCustomerEligibleAdvancesQueryHandler(_banks);
        var result = await handler.HandleAsync(
            new GetCustomerEligibleAdvancesQuery(_companyId, _customer.Id));

        var single = Assert.Single(result);
        Assert.Equal(eligible.Id, single.PaymentEntryId);
        Assert.Equal(150m, single.UnallocatedAmount);
    }
}
