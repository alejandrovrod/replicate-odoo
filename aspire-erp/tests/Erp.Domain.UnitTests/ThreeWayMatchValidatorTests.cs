using Erp.Domain.Exceptions;
using Erp.Domain.Services;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Task 4.4 acceptance ("Rejects invoices exceeding received quantity with
/// <see cref="OverbillingNotAllowedException"/>") - the cumulative three-way match of
/// <see cref="ThreeWayMatchValidator.ValidateBillingQuantity"/> per spec BY-03 and BY-06: the
/// ceiling is <c>receivedQty - previouslyBilledQty</c>, the two rejection messages are the VERBATIM
/// literals the scenarios quote, and the boundary quantities pass.
/// </summary>
/// <remarks>
/// Pure C#, no EF Core and no database (Constitution I.2/I.3). The <c>.0000</c> case exists
/// because quantities arrive as <c>decimal(18,4)</c> from SQL Server: the message must read
/// <c>10</c>, not <c>10.0000</c>.
/// </remarks>
public sealed class ThreeWayMatchValidatorTests
{
    /// <summary>Spec BY-03 literal: 10 received, nothing billed yet, 15 attempted.</summary>
    private const string By03Message = "Cannot bill 15 units. Maximum receivable: 10";

    /// <summary>Spec BY-06 literal: 20 received, 15 billed by the first clerk, 15 attempted again.</summary>
    private const string By06Message = "Only 5 units remaining to bill, 15 requested";

    // ------------------------------------------------------------------ rejections (BY-03/BY-06)

    [Fact]
    public void ValidateBillingQuantity_FirstBillAboveReceived_ThrowsBy03Message()
    {
        var ex = Assert.Throws<OverbillingNotAllowedException>(
            () => ThreeWayMatchValidator.ValidateBillingQuantity(
                receivedQty: 10m, previouslyBilledQty: 0m, attemptingToBillQty: 15m));

        Assert.Equal(By03Message, ex.Message);
    }

    [Fact]
    public void ValidateBillingQuantity_SecondConcurrentBillAboveRemaining_ThrowsBy06Message()
    {
        // Spec BY-06: the first clerk billed 15 of the 20 received; the second asks for 15 more.
        var ex = Assert.Throws<OverbillingNotAllowedException>(
            () => ThreeWayMatchValidator.ValidateBillingQuantity(
                receivedQty: 20m, previouslyBilledQty: 15m, attemptingToBillQty: 15m));

        Assert.Equal(By06Message, ex.Message);
    }

    [Fact]
    public void ValidateBillingQuantity_BillOnFullyBilledReceipt_ThrowsBy06FormWithZeroRemaining()
    {
        var ex = Assert.Throws<OverbillingNotAllowedException>(
            () => ThreeWayMatchValidator.ValidateBillingQuantity(
                receivedQty: 20m, previouslyBilledQty: 20m, attemptingToBillQty: 1m));

        Assert.Equal("Only 0 units remaining to bill, 1 requested", ex.Message);
    }

    [Fact]
    public void ValidateBillingQuantity_DatabaseScaleDecimals_RendersFourScaleFractionsWithoutTails()
    {
        // 10.0000 received - 5.0000 billed leaves 5.0000; the attempt of 5.5000 must read as
        // "5.5", never "5.5000" (spec literals are written with plain numbers).
        var ex = Assert.Throws<OverbillingNotAllowedException>(
            () => ThreeWayMatchValidator.ValidateBillingQuantity(
                receivedQty: 10.0000m, previouslyBilledQty: 5.0000m, attemptingToBillQty: 5.5000m));

        Assert.Equal("Only 5 units remaining to bill, 5.5 requested", ex.Message);
        Assert.DoesNotContain("0000", ex.Message);
    }

    // ------------------------------------------------------------------------------- acceptances

    [Fact]
    public void ValidateBillingQuantity_BillEqualToRemaining_DoesNotThrow()
    {
        // 20 received - 15 billed = 5 billable; asking for exactly 5 is the boundary that passes.
        ThreeWayMatchValidator.ValidateBillingQuantity(
            receivedQty: 20m, previouslyBilledQty: 15m, attemptingToBillQty: 5m);
    }

    [Fact]
    public void ValidateBillingQuantity_FullMatchOnFirstBill_DoesNotThrow()
    {
        // Spec BY-03 is a STRICT inequality against overbilling: billed == received is legal.
        ThreeWayMatchValidator.ValidateBillingQuantity(
            receivedQty: 10m, previouslyBilledQty: 0m, attemptingToBillQty: 10m);
    }
}
