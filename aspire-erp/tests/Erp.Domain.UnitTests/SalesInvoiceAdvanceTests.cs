using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Sales-invoice ERPNext parity (spec scenarios SI-G2/SI-G3): the Draft-time advance
/// reservation on <see cref="SalesInvoice.AllocateAdvance"/> - the running cap against
/// <c>GrandTotal</c>, the <c>TotalAdvance / OutstandingAmount</c> consistency and the
/// Draft-only guard.
/// </summary>
public sealed class SalesInvoiceAdvanceTests
{
    private static SalesInvoice NewDraftInvoice(decimal grandTotal = 200m) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            InvoiceNumber = "SINV-2026-00001",
            CustomerId = Guid.NewGuid(),
            PostingDate = new DateOnly(2026, 10, 15),
            DueDate = new DateOnly(2026, 11, 14),
            Status = SalesInvoiceStatus.Draft,
            NetTotal = grandTotal,
            TaxTotal = 0m,
            GrandTotal = grandTotal,
            OutstandingAmount = grandTotal,
        };

    [Fact]
    public void AllocateAdvance_PartialAmount_ReducesOutstanding()
    {
        var invoice = NewDraftInvoice(grandTotal: 200m);

        invoice.AllocateAdvance(Guid.NewGuid(), 150m);

        Assert.Equal(150m, invoice.TotalAdvance);
        Assert.Equal(50m, invoice.OutstandingAmount);
        var slice = Assert.Single(invoice.Advances);
        Assert.Equal(150m, slice.AllocatedAmount);
        Assert.Equal(invoice.Id, slice.SalesInvoiceId);
        Assert.Equal("SalesInvoice", slice.ReferenceDocumentType);
    }

    [Fact]
    public void AllocateAdvance_LandingExactlyOnTotal_Allows()
    {
        var invoice = NewDraftInvoice(grandTotal: 200m);

        invoice.AllocateAdvance(Guid.NewGuid(), 200m);

        Assert.Equal(200m, invoice.TotalAdvance);
        Assert.Equal(0m, invoice.OutstandingAmount);
    }

    [Fact]
    public void AllocateAdvance_ProgressiveSlices_AccumulateAgainstTheCap()
    {
        var invoice = NewDraftInvoice(grandTotal: 200m);

        invoice.AllocateAdvance(Guid.NewGuid(), 120m);
        invoice.AllocateAdvance(Guid.NewGuid(), 80m);

        Assert.Equal(200m, invoice.TotalAdvance);
        Assert.Equal(0m, invoice.OutstandingAmount);
        Assert.Equal(2, invoice.Advances.Count);
    }

    [Fact]
    public void AllocateAdvance_StepBeyondTotal_ThrowsWithAdvanceExceedsCode()
    {
        var invoice = NewDraftInvoice(grandTotal: 200m);

        var ex = Assert.Throws<SalesValidationException>(
            () => invoice.AllocateAdvance(Guid.NewGuid(), 500m));

        Assert.Equal(SellingErrorCodes.AdvanceExceedsInvoiceTotal, ex.Code);
        Assert.Equal("advance_exceeds_invoice_total", ex.Code);
        Assert.Equal(0m, invoice.TotalAdvance);
        Assert.Empty(invoice.Advances);
    }

    [Fact]
    public void AllocateAdvance_SecondSliceBeyondTotal_ThrowsAndKeepsFirstSlice()
    {
        var invoice = NewDraftInvoice(grandTotal: 200m);
        invoice.AllocateAdvance(Guid.NewGuid(), 150m);

        Assert.Throws<SalesValidationException>(
            () => invoice.AllocateAdvance(Guid.NewGuid(), 100m));

        Assert.Equal(150m, invoice.TotalAdvance);
        Assert.Single(invoice.Advances);
    }

    [Fact]
    public void AllocateAdvance_OnSubmittedInvoice_ThrowsInvalidOperation()
    {
        var invoice = NewDraftInvoice();
        invoice.Status = SalesInvoiceStatus.Unpaid;

        Assert.Throws<InvalidOperationException>(
            () => invoice.AllocateAdvance(Guid.NewGuid(), 10m));
    }
}
