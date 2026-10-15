using Erp.Application.Features.Payments.Queries;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Detail read behind the payment dialog (ui-spec r-12 D1): one voucher with its allocation
/// slices and the GL rows it posted. A Draft posted nothing yet, so its GL list must be empty
/// rather than null; the read is also company-scoped so a foreign tenant sees nothing.
/// </summary>
public sealed class PaymentDetailQueryHandlerTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly FakeBankRepository _banks = new();
    private readonly FakeStockRepository _stock = new();

    private GetPaymentDetailQueryHandler Handler() => new(_banks, _stock);

    private PaymentEntry Seed(int allocations, bool submitted)
    {
        var payment = new PaymentEntry
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            BankAccountId = Guid.NewGuid(),
            PaymentType = PaymentType.Receive,
            PartyType = PaymentPartyType.Customer,
            PartyId = Guid.NewGuid(),
            PartyName = "ACME Corp",
            PaymentDate = new DateOnly(2026, 10, 5),
            PaidAmount = 100m,
            UnallocatedAmount = 100m - (allocations * 30m),
            DocumentStatus = submitted ? PaymentDocumentStatus.Submitted : PaymentDocumentStatus.Draft,
            RowVersion = [1, 2, 3, 4],
            CreatedAt = DateTimeOffset.UtcNow,
        };

        for (var i = 0; i < allocations; i++)
        {
            payment.Allocations.Add(new PaymentAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = payment.TenantId,
                PaymentEntryId = payment.Id,
                SalesInvoiceId = Guid.NewGuid(),
                ReferenceDocumentType = "SalesInvoice",
                ReferenceDocumentId = Guid.NewGuid(),
                AllocatedAmount = 30m * (i + 1),
                TotalAmount = 90m,
                OutstandingAmount = 30m * (i + 1),
                ExchangeRate = 1m,
            });
        }

        _banks.SeedPaymentEntry(payment);
        return payment;
    }

    private Task SeedGlAsync(PaymentEntry payment)
    {
        var bank = new Account { Id = Guid.NewGuid(), AccountCode = "1100", AccountName = "Cash - Bank" };
        var receivable = new Account { Id = Guid.NewGuid(), AccountCode = "1200", AccountName = "Accounts Receivable" };

        return _stock.AddGlEntriesAsync(
        [
            new GLEntry
            {
                Id = 1,
                CompanyId = _companyId,
                VoucherId = payment.Id,
                VoucherType = "PaymentEntry",
                VoucherNo = "PAY-2026-00001",
                AccountId = bank.Id,
                Account = bank,
                PostingDate = payment.PaymentDate,
                Debit = 100m,
                Credit = 0m,
                Remarks = "Dr Bank",
                CreatedAt = DateTimeOffset.UtcNow,
            },
            new GLEntry
            {
                Id = 2,
                CompanyId = _companyId,
                VoucherId = payment.Id,
                VoucherType = "PaymentEntry",
                VoucherNo = "PAY-2026-00001",
                AccountId = receivable.Id,
                Account = receivable,
                PostingDate = payment.PaymentDate,
                Debit = 0m,
                Credit = 100m,
                Remarks = "Cr Customer",
                CreatedAt = DateTimeOffset.UtcNow,
            },
        ]);
    }

    [Fact]
    public async Task SubmittedVoucher_ReturnsHeaderAllocationsAndGl()
    {
        var payment = Seed(allocations: 2, submitted: true);
        await SeedGlAsync(payment);

        var detail = await Handler().HandleAsync(new GetPaymentDetailQuery(_companyId, payment.Id));

        Assert.NotNull(detail);
        Assert.Equal(payment.Id, detail!.Payment.Id);
        Assert.Equal("ACME Corp", detail.Payment.PartyName);

        Assert.Equal(2, detail.Allocations.Count);
        // Handler orders slices by allocated amount ascending.
        Assert.Equal(30m, detail.Allocations[0].AllocatedAmount);
        Assert.Equal(60m, detail.Allocations[1].AllocatedAmount);

        Assert.Equal(2, detail.GlEntries.Count);
        Assert.Equal("1100", detail.GlEntries[0].AccountCode);
        Assert.Equal("Cash - Bank", detail.GlEntries[0].AccountName);
        Assert.Equal(100m, detail.GlEntries[0].Debit);
        Assert.Equal("1200", detail.GlEntries[1].AccountCode);
        Assert.Equal(100m, detail.GlEntries[1].Credit);
    }

    [Fact]
    public async Task DraftVoucher_HasAllocationsButNoGl()
    {
        var payment = Seed(allocations: 1, submitted: false);

        var detail = await Handler().HandleAsync(new GetPaymentDetailQuery(_companyId, payment.Id));

        Assert.NotNull(detail);
        Assert.Empty(detail!.GlEntries);
        Assert.Single(detail.Allocations);
    }

    [Fact]
    public async Task UnknownOrForeignCompany_ReturnsNull()
    {
        var payment = Seed(allocations: 0, submitted: true);

        Assert.Null(await Handler().HandleAsync(new GetPaymentDetailQuery(_companyId, Guid.NewGuid())));
        Assert.Null(await Handler().HandleAsync(new GetPaymentDetailQuery(Guid.NewGuid(), payment.Id)));
    }
}
