using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Services;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Task 5.1 acceptance ("Credit calculation tests verify that breaches throw
/// <see cref="CreditLimitExceededException"/>") - the FULL boundary matrix of
/// <see cref="CreditControlEvaluator.ValidateCreditExposure"/> per spec SL-02 and plan.md §2:
/// exposure within the limit and exactly AT the limit pass, exposure over the limit throws,
/// <c>BypassCreditLimitCheck</c> and a zero limit short-circuit the check, and
/// <c>OutstandingAmount</c> participates in the exposure.
/// </summary>
public sealed class CreditControlEvaluatorTests
{
    /// <summary>ACME Corp of spec SL-02: $5,000.00 limit against $4,600.00 outstanding debt.</summary>
    private static Customer CustomerWith(
        decimal creditLimit,
        decimal outstandingAmount,
        bool bypassCreditLimitCheck = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            CustomerCode = "ACME",
            CustomerName = "ACME Corp",
            CreditLimit = creditLimit,
            OutstandingAmount = outstandingAmount,
            BypassCreditLimitCheck = bypassCreditLimitCheck,
        };

    [Fact]
    public void ValidateCreditExposure_ExposureWithinLimit_Allows()
    {
        // 1,000 outstanding + 3,000 attempted = 4,000 <= 5,000.
        var customer = CustomerWith(creditLimit: 5000m, outstandingAmount: 1000m);

        CreditControlEvaluator.ValidateCreditExposure(customer, newInvoiceAmount: 3000m); // must not throw
    }

    [Fact]
    public void ValidateCreditExposure_ExposureOverLimit_ThrowsCreditLimitExceeded()
    {
        // Spec SL-02 literally: limit $5,000, outstanding $4,600, attempt $650 -> $5,250.
        var customer = CustomerWith(creditLimit: 5000m, outstandingAmount: 4600m);

        var ex = Assert.Throws<CreditLimitExceededException>(
            () => CreditControlEvaluator.ValidateCreditExposure(customer, newInvoiceAmount: 650m));

        // The exception carries the stable wire code plus the three numbers of the spec message.
        Assert.Equal(SellingErrorCodes.CreditLimitExceeded, ex.Code);
        Assert.Equal("ACME Corp", ex.CustomerName);
        Assert.Equal(5000m, ex.CreditLimit);
        Assert.Equal(4600m, ex.OutstandingAmount);
        Assert.Equal(650m, ex.AttemptedAmount);
    }

    [Fact]
    public void ValidateCreditExposure_ExactlyAtLimit_Allows()
    {
        // plan.md §2 rejects only `totalExposure > CreditLimit` (spec SL-02's `<=`): landing
        // exactly ON the limit is a legal exposure.
        var customer = CustomerWith(creditLimit: 5000m, outstandingAmount: 4600m);

        CreditControlEvaluator.ValidateCreditExposure(customer, newInvoiceAmount: 400m); // 5,000 == 5,000

        var fresh = CustomerWith(creditLimit: 5000m, outstandingAmount: 0m);
        CreditControlEvaluator.ValidateCreditExposure(fresh, newInvoiceAmount: 5000m);
    }

    [Fact]
    public void ValidateCreditExposure_BypassCreditLimitCheckSet_NeverThrows()
    {
        // Spec SL-02 rejects only when BypassCreditLimitCheck == false - any exposure passes.
        var customer = CustomerWith(
            creditLimit: 5000m,
            outstandingAmount: 100000m,
            bypassCreditLimitCheck: true);

        CreditControlEvaluator.ValidateCreditExposure(customer, newInvoiceAmount: 999999m); // must not throw
    }

    [Fact]
    public void ValidateCreditExposure_ZeroLimit_NeverThrows()
    {
        // plan.md §2 `CreditLimit <= 0` short-circuit: a zero limit means NO credit control for
        // that customer (the permissive reading - CK_Customer_CreditLimit keeps the value from
        // ever going negative in the database).
        var customer = CustomerWith(creditLimit: 0m, outstandingAmount: 100000m);

        CreditControlEvaluator.ValidateCreditExposure(customer, newInvoiceAmount: 5000m); // must not throw
    }

    [Fact]
    public void ValidateCreditExposure_OutstandingAmountParticipatesInExposure()
    {
        // The attempted amount alone (200) is far below the limit; only because the booked debt
        // (4,900) counts too does 5,100 breach 5,000 - the spec's Outstanding Debt + attempt.
        var customer = CustomerWith(creditLimit: 5000m, outstandingAmount: 4900m);

        Assert.Throws<CreditLimitExceededException>(
            () => CreditControlEvaluator.ValidateCreditExposure(customer, newInvoiceAmount: 200m));
    }

    [Fact]
    public void CreditLimitExceededException_CarriesStableSnakeCaseErrorCode()
    {
        // The wire contract for the RFC 7807 `code` extension (mirrors the
        // FiscalPeriodLockedException "fiscal_period_locked" assertion).
        var customer = CustomerWith(creditLimit: 5000m, outstandingAmount: 4600m);

        var ex = Assert.Throws<CreditLimitExceededException>(
            () => CreditControlEvaluator.ValidateCreditExposure(customer, 650m));

        Assert.Equal("credit_limit_exceeded", ex.Code);

        // Spec SL-02 message shape: "Credit limit ... exceeded. Current: ..., Attempted: ...".
        Assert.Contains("Credit limit", ex.Message);
        Assert.Contains("exceeded", ex.Message);
        Assert.Contains("Current", ex.Message);
        Assert.Contains("Attempted", ex.Message);
    }

    [Fact]
    public void ValidateCreditExposure_NullCustomer_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => CreditControlEvaluator.ValidateCreditExposure(null!, newInvoiceAmount: 100m));
    }
}
