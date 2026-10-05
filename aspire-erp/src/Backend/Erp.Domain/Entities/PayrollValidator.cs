using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure C# shape rules for the payroll batch engine (Tasks 12.3-12.4). Mirrors the
/// <see cref="SalaryStructureValidator"/> static-guard style: period sanity and payment-day
/// ranges live here; eligibility itself stays on <see cref="Employee.IsEligibleForPeriod"/>
/// (spec HR-03, Block A).
/// </summary>
public static class PayrollValidator
{
    /// <summary>
    /// Attendance denominator: payment days are counted against a standard 30-day month
    /// (proration factor = PaymentDays / 30). There is no attendance/leave module - PaymentDays
    /// and AbsentDays arrive as run inputs.
    /// </summary>
    public const int StandardMonthDays = 30;

    /// <summary>Payroll period sanity: the end date must not precede the start date (both inclusive).</summary>
    /// <exception cref="HrValidationException">The period is inverted (<c>invalid_payroll_period</c>).</exception>
    public static void EnsureValidPeriod(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.InvalidPayrollPeriod,
                $"Payroll period end ({endDate:yyyy-MM-dd}) must not precede period start ({startDate:yyyy-MM-dd}).");
        }
    }

    /// <summary>
    /// Payment-day ranges: each side sits inside [0, 30]. No sum constraint is enforced -
    /// proration keys off PaymentDays only, AbsentDays is informational.
    /// </summary>
    /// <exception cref="HrValidationException">A day count is out of range (<c>invalid_payment_days</c>).</exception>
    public static void EnsureValidPaymentDays(int paymentDays, int absentDays)
    {
        if (paymentDays < 0 || paymentDays > StandardMonthDays)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.InvalidPaymentDays,
                $"Payment days must sit inside [0, {StandardMonthDays}] (received {paymentDays}).");
        }

        if (absentDays < 0 || absentDays > StandardMonthDays)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.InvalidPaymentDays,
                $"Absent days must sit inside [0, {StandardMonthDays}] (received {absentDays}).");
        }
    }
}
