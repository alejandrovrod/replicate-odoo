using Erp.Domain.Common;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Batch payroll workflow (Tasks 12.3-12.4, spec ubiquitous language "Payroll Entry"):
/// Draft -&gt; Submitted (accrual posted) -&gt; Paid (disbursed), with Cancelled reachable from
/// Submitted only. Follows the <see cref="WorkOrder"/> state-machine precedent (transitions throw
/// typed domain failures; handlers map them to 409).
/// </summary>
public enum PayrollEntryStatus
{
    Draft,
    Submitted,
    Paid,
    Cancelled,
}

/// <summary>
/// Monthly payroll batch header (Task 12.3): one row per company-period run, holding the summed
/// slip totals and the links to its two GL vouchers. Totals are STORED, not computed: spec
/// invariant HR-01 floors net pay at zero (Math.Max), and a computed column cannot represent
/// that floor (Block A deviation, recorded).
/// </summary>
/// <remarks>
/// The gapless <see cref="PayrollNumber"/> (PE-YYYY-NNNNN) is assigned inside the submit
/// transaction, mirroring the work-order precedent. <see cref="AccrualVoucherNo"/> and
/// <see cref="PaymentVoucherNo"/> are informational voucher-number links (there is no GL header
/// table - GLEntry rows carry VoucherId = this entry's id); the disbursement boundary holds: no
/// banking PaymentEntry voucher is created, only the Dr 2150 / Cr bank GL pair (documented).
/// </remarks>
public class PayrollEntry : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    /// <summary>Gapless batch number (Constitution III.4): PE-2026-00001, assigned at submit.</summary>
    public string PayrollNumber { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    /// <summary>Accounting date of the accrual voucher (freeze-gated, spec AC-04).</summary>
    public DateOnly PostingDate { get; set; }

    public PayrollEntryStatus Status { get; set; } = PayrollEntryStatus.Draft;

    /// <summary>Sum of slip gross pay (decimal(18,4), stored).</summary>
    public decimal TotalGrossPay { get; set; }

    /// <summary>Sum of slip deductions (decimal(18,4), stored).</summary>
    public decimal TotalDeductions { get; set; }

    /// <summary>Sum of slip net pay (decimal(18,4), stored, clamped at zero per slip).</summary>
    public decimal TotalNetPay { get; set; }

    /// <summary>Accrual voucher number (PYR-YYYY-NNNNN), stamped at submit. Null while Draft.</summary>
    public string? AccrualVoucherNo { get; set; }

    /// <summary>Disbursement voucher number (PYR-YYYY-NNNNN), stamped at disburse. Null until Paid.</summary>
    public string? PaymentVoucherNo { get; set; }

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c>): submit/disburs/cancel are
    /// read-modify-write, so EF puts the original value in the UPDATE ... WHERE clause and a
    /// concurrent transition throws instead of being silently lost. Store-generated.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<SalarySlip> Slips { get; set; } = new List<SalarySlip>();

    /// <summary>
    /// Draft -&gt; Submitted: slips are generated and the accrual voucher is posted. Called by
    /// the submit handler only after the voucher balances and persists.
    /// </summary>
    /// <exception cref="HrValidationException">The entry is not a Draft (<c>invalid_status_transition</c>).</exception>
    public void Submit()
    {
        if (Status != PayrollEntryStatus.Draft)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.InvalidStatusTransition,
                $"Only a Draft payroll entry can be submitted; entry '{PayrollNumber}' is '{Status}'.");
        }

        Status = PayrollEntryStatus.Submitted;
    }

    /// <summary>
    /// Submitted -&gt; Paid: the Dr 2150 / Cr bank disbursement pair is posted. Called by the
    /// disburse handler only after the pair balances and persists.
    /// </summary>
    /// <exception cref="HrValidationException">The entry is not Submitted (<c>invalid_status_transition</c>).</exception>
    public void MarkPaid()
    {
        if (Status != PayrollEntryStatus.Submitted)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.InvalidStatusTransition,
                $"Only a Submitted payroll entry can be disbursed; entry '{PayrollNumber}' is '{Status}'.");
        }

        Status = PayrollEntryStatus.Paid;
    }

    /// <summary>
    /// Submitted -&gt; Cancelled (spec HR-05): the accrual voucher is mirrored and the slips are
    /// cancelled. Paid entries (money already left) and Draft entries (nothing posted yet) are
    /// rejected - the handler reports both as 409.
    /// </summary>
    /// <exception cref="HrValidationException">The entry is not Submitted (<c>invalid_status_transition</c>).</exception>
    public void Cancel()
    {
        if (Status != PayrollEntryStatus.Submitted)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.InvalidStatusTransition,
                $"Only a Submitted payroll entry can be cancelled; entry '{PayrollNumber}' is '{Status}'.");
        }

        Status = PayrollEntryStatus.Cancelled;
    }
}
