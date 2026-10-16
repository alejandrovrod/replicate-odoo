using Erp.Application.Features.Buying.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Module buying-suppliers, spec §1 gap D4 / tasks.md T2.3 (scenarios SUP-G2): the governance
/// guard rejects a bill for a supplier on hold for invoices BEFORE the posting engine runs, so the
/// failure carries zero ledger writes and maps to 422 <c>supplier_on_hold_invoices</c>.
/// </summary>
public sealed class PostPurchaseInvoiceHoldGuardTests
{
    private readonly FakeSupplierRepository _suppliers = new();
    private readonly FakePurchasePostingService _posting = new();

    private PostPurchaseInvoiceCommandHandler Handler() => new(_posting, _suppliers);

    private Supplier SeedSupplier(bool onHold, SupplierHoldType holdType, DateOnly? releaseDate = null, bool isActive = true)
    {
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Code = "SUP-001",
            Name = "Acme Industrial Supplies",
            IsActive = isActive,
            OnHold = onHold,
            HoldType = holdType,
            ReleaseDate = releaseDate,
        };
        _suppliers.Seed(supplier);
        return supplier;
    }

    private static PostPurchaseInvoiceCommand Bill(Guid supplierId, DateOnly postingDate) =>
        new(Guid.NewGuid(), supplierId, "BILL-001", PostingDate: postingDate);

    [Fact]
    public async Task Post_SupplierOnHoldForInvoices_RejectedBeforePosting()
    {
        var supplier = SeedSupplier(onHold: true, holdType: SupplierHoldType.Invoices, releaseDate: new DateOnly(2026, 12, 31));

        var result = await Handler().HandleAsync(Bill(supplier.Id, new DateOnly(2026, 10, 15)));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.SupplierOnHoldInvoices, result.Error!.Code);
        Assert.False(_posting.PostInvoiceCalled);
    }

    [Fact]
    public async Task Post_SupplierOnHoldForAll_AlsoRejected()
    {
        var supplier = SeedSupplier(onHold: true, holdType: SupplierHoldType.All);

        var result = await Handler().HandleAsync(Bill(supplier.Id, new DateOnly(2026, 10, 15)));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.SupplierOnHoldInvoices, result.Error!.Code);
        Assert.False(_posting.PostInvoiceCalled);
    }

    [Fact]
    public async Task Post_SupplierOnHoldForPayments_StillAllowsBilling()
    {
        // A payments-scoped hold does not block invoices; the guard lets the request reach posting
        // (the strict double then throws, which proves the guard did not reject it).
        var supplier = SeedSupplier(onHold: true, holdType: SupplierHoldType.Payments);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Handler().HandleAsync(Bill(supplier.Id, new DateOnly(2026, 10, 15))));

        Assert.True(_posting.PostInvoiceCalled);
    }

    [Fact]
    public async Task Post_UnknownSupplier_FailsWithSupplierNotFound()
    {
        var result = await Handler().HandleAsync(Bill(Guid.NewGuid(), new DateOnly(2026, 10, 15)));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.SupplierNotFound, result.Error!.Code);
        Assert.False(_posting.PostInvoiceCalled);
    }
}
