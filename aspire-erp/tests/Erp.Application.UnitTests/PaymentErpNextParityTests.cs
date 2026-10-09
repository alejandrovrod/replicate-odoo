using Erp.Application.Features.Payments.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Phase 6 ERPNext parity on payment vouchers: draft capture of the new header fields,
/// Employee advances, internal-transfer leg validation and allocation snapshots
/// (ReferenceDocumentType/Total/Outstanding/ExchangeRate), through the CQRS handler.
/// </summary>
public sealed class PaymentErpNextParityTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _bankAccountId = Guid.NewGuid();
    private readonly FakeBankRepository _banks = new();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeCustomerRepository _customers = new();
    private readonly FakeSupplierRepository _suppliers = new();
    private readonly FakeSalesInvoiceRepository _salesInvoices = new();
    private readonly FakePurchaseRepository _purchases = new();

    private readonly Customer _customer;
    private readonly SalesInvoice _invoice;

    public PaymentErpNextParityTests()
    {
        _companies.Company = new Company { Id = _companyId, TenantId = Guid.NewGuid(), Name = "Acme" };
        _banks.SeedAccount(new BankAccount
        {
            Id = _bankAccountId,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            AccountName = "Checking",
            BankName = "Acme Bank",
            AccountNumber = "123456",
            GLAccountId = Guid.NewGuid(),
            IsActive = true,
        });

        _customer = new Customer
        {
            Id = Guid.NewGuid(),
            CompanyId = _companyId,
            CustomerCode = "CUST-001",
            CustomerName = "ACME Corp",
            IsActive = true,
        };
        _customers.Seed(_customer);

        _invoice = new SalesInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            InvoiceNumber = "SINV-2026-00001",
            CustomerId = _customer.Id,
            PostingDate = new DateOnly(2026, 10, 1),
            DueDate = new DateOnly(2026, 10, 31),
            Status = SalesInvoiceStatus.Unpaid,
            NetTotal = 100m,
            GrandTotal = 100m,
            OutstandingAmount = 100m,
            ExchangeRate = 2m,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _salesInvoices.Seed(_invoice);
    }

    private CreatePaymentEntryCommandHandler Handler() =>
        new(_banks, _companies, _customers, _suppliers, _salesInvoices, _purchases);

    private static CreatePaymentEntryCommand Draft(
        Guid companyId,
        Guid bankAccountId,
        Guid partyId,
        PaymentType type = PaymentType.Receive,
        PaymentPartyType party = PaymentPartyType.Customer) =>
        new(companyId, type, party, partyId, bankAccountId, new DateOnly(2026, 10, 5), 100m, null, null, []);

    [Fact]
    public async Task Create_HeaderParityFields_PersistAndDefaultPartyName()
    {
        var result = await Handler().HandleAsync(Draft(_companyId, _bankAccountId, _customer.Id) with
        {
            ModeOfPayment = " Wire ",
            Remarks = " October collection ",
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("Wire", result.Value!.ModeOfPayment);
        Assert.Equal("October collection", result.Value.Remarks);
        Assert.Equal("ACME Corp", result.Value.PartyName); // defaulted from the customer master
        Assert.Equal(100m, result.Value.ReceivedAmount); // defaults to the paid amount
        Assert.Equal(1m, result.Value.SourceExchangeRate);
    }

    [Fact]
    public async Task Create_AllocationSnapshots_StampedFromLiveInvoice()
    {
        var result = await Handler().HandleAsync(
            Draft(_companyId, _bankAccountId, _customer.Id) with
            {
                Allocations = [new PaymentAllocationInput(_invoice.Id, null, 40m)],
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(60m, result.Value!.UnallocatedAmount);

        var saved = Assert.Single(_banks.AddedPayments);
        var slice = Assert.Single(saved.Allocations);
        Assert.Equal("SalesInvoice", slice.ReferenceDocumentType);
        Assert.Equal(_invoice.Id, slice.ReferenceDocumentId);
        Assert.Equal(100m, slice.TotalAmount);
        Assert.Equal(100m, slice.OutstandingAmount); // live balance snapshot at draft time
        Assert.Equal(2m, slice.ExchangeRate);
    }

    [Fact]
    public async Task Create_EmployeeAdvance_AcceptedAsDraft()
    {
        var result = await Handler().HandleAsync(
            Draft(_companyId, _bankAccountId, Guid.NewGuid(), PaymentType.Pay, PaymentPartyType.Employee));

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentPartyType.Employee, result.Value!.PartyType);
        Assert.Equal(100m, result.Value.UnallocatedAmount); // whole amount stays as advance
    }

    [Fact]
    public async Task Create_InternalTransferWithoutLegs_Rejected()
    {
        var result = await Handler().HandleAsync(
            Draft(_companyId, _bankAccountId, Guid.Empty, PaymentType.InternalTransfer, PaymentPartyType.Customer));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Create_InternalTransferWithLegs_AcceptedAsDraft()
    {
        var result = await Handler().HandleAsync(
            Draft(_companyId, _bankAccountId, Guid.Empty, PaymentType.InternalTransfer, PaymentPartyType.Customer) with
            {
                PaidFromAccountId = Guid.NewGuid(),
                PaidToAccountId = Guid.NewGuid(),
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentType.InternalTransfer, result.Value!.PaymentType);
    }
}
