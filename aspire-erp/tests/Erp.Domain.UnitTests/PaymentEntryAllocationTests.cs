using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Task 6.1 acceptance (anti-overpayment invariant
/// <c>alreadyAllocated + amount &lt;= invoiceOutstanding</c>): over-allocation throws with the
/// stable code and the three money values, landing exactly ON the outstanding balance is
/// allowed, progressive allocations accumulate against the same cap, and non-positive steps
/// are rejected without touching the cap.
/// </summary>
public sealed class PaymentEntryAllocationTests
{
    private static PaymentEntry NewPayment() =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            BankAccountId = Guid.NewGuid(),
            PaymentType = PaymentType.Receive,
            PaymentDate = new DateOnly(2026, 10, 2),
            PaidAmount = 1000m,
        };

    [Fact]
    public void Allocate_StepBeyondOutstanding_ThrowsWithCodeAndFields()
    {
        var payment = NewPayment();

        var ex = Assert.Throws<BankingValidationException>(
            () => payment.Allocate(amount: 200m, invoiceOutstanding: 1000m, alreadyAllocated: 900m));

        Assert.Equal(BankingErrorCodes.OverAllocation, ex.Code);
        Assert.Equal("over_allocation", ex.Code);
        Assert.Equal(200m, ex.AttemptedAmount);
        Assert.Equal(1000m, ex.InvoiceOutstanding);
        Assert.Equal(900m, ex.AlreadyAllocated);
        Assert.Contains("outstanding balance", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Allocate_LandingExactlyOnOutstanding_Allows()
    {
        var payment = NewPayment();

        // 900 already allocated + 100 lands exactly ON the 1,000 cap: legal (the <= boundary).
        payment.Allocate(amount: 100m, invoiceOutstanding: 1000m, alreadyAllocated: 900m);

        var fresh = NewPayment();
        fresh.Allocate(amount: 1000m, invoiceOutstanding: 1000m, alreadyAllocated: 0m);
    }

    [Fact]
    public void Allocate_ProgressiveSteps_AccumulateAgainstTheCap()
    {
        var payment = NewPayment();
        const decimal outstanding = 1000m;
        var allocated = 0m;

        payment.Allocate(600m, outstanding, allocated);
        allocated += 600m;
        payment.Allocate(300m, outstanding, allocated);
        allocated += 300m;

        // Only 100 remains: a 101 step breaches the cap even though each step alone is small.
        var ex = Assert.Throws<BankingValidationException>(
            () => payment.Allocate(101m, outstanding, allocated));
        Assert.Equal(BankingErrorCodes.OverAllocation, ex.Code);

        // The exact remainder still fits.
        payment.Allocate(100m, outstanding, allocated);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Allocate_ZeroOrNegativeAmount_Rejected(decimal amount)
    {
        var payment = NewPayment();

        var ex = Assert.Throws<BankingValidationException>(
            () => payment.Allocate(amount, invoiceOutstanding: 1000m, alreadyAllocated: 0m));

        Assert.Equal(BankingErrorCodes.InvalidAllocationAmount, ex.Code);
        Assert.Null(ex.AttemptedAmount);
    }
}
