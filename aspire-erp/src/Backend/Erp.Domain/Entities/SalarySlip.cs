using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Individual pay stub workflow (Task 12.3, spec ubiquitous language "Salary Slip"):
/// Draft -&gt; Submitted with the batch, Cancelled only through the batch cancel (spec HR-05).
/// </summary>
public enum SalarySlipStatus
{
    Draft,
    Submitted,
    Cancelled,
}

/// <summary>
/// One employee's pay stub of a <see cref="PayrollEntry"/> (Task 12.3, spec scenario HR-01):
/// gross earnings, itemized statutory deductions (see <see cref="SalarySlipLine"/>) and net pay.
/// Amounts are STORED, clamped at zero (spec invariant HR-01 floor).
/// </summary>
/// <remarks>
/// Aggregate child of <see cref="PayrollEntry"/> (the BomItem precedent): no TenantId of its
/// own, reached through the entry. The (PayrollEntryId, EmployeeId) pair is DB-unique (spec
/// HR-06 - exactly one slip per employee per run); the database is the authority, the
/// repository pre-check is 409 UX only.
/// </remarks>
public class SalarySlip
{
    public Guid Id { get; set; }

    public Guid PayrollEntryId { get; set; }

    public PayrollEntry? PayrollEntry { get; set; }

    public Guid EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    /// <summary>Deterministic stub number within the run: {PayrollNumber}-{index:D3}.</summary>
    public string SlipNumber { get; set; } = string.Empty;

    /// <summary>Payable days of the period (attendance/leave input arrives as a run input - no attendance module). Defaults to 30.</summary>
    public int PaymentDays { get; set; } = PayrollValidator.StandardMonthDays;

    /// <summary>Absent days of the period (informational - proration keys off PaymentDays only). Defaults to 0.</summary>
    public int AbsentDays { get; set; }

    /// <summary>Sum of earning lines (decimal(18,4), stored).</summary>
    public decimal GrossPay { get; set; }

    /// <summary>Sum of deduction lines (decimal(18,4), stored).</summary>
    public decimal TotalDeductions { get; set; }

    /// <summary>GrossPay - TotalDeductions, floored at zero (decimal(18,4), stored).</summary>
    public decimal NetPay { get; set; }

    public SalarySlipStatus Status { get; set; } = SalarySlipStatus.Draft;

    /// <summary>Optimistic concurrency token (SQL Server <c>rowversion</c>). Store-generated.</summary>
    public byte[] RowVersion { get; set; } = null!;

    public ICollection<SalarySlipLine> Lines { get; set; } = new List<SalarySlipLine>();

    /// <summary>
    /// Draft -&gt; Submitted: the batch submitter calls this as each slip is priced.
    /// </summary>
    /// <exception cref="HrValidationException">The slip is not a Draft (<c>invalid_status_transition</c>).</exception>
    public void Submit()
    {
        if (Status != SalarySlipStatus.Draft)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.InvalidStatusTransition,
                $"Only a Draft salary slip can be submitted; slip '{SlipNumber}' is '{Status}'.");
        }

        Status = SalarySlipStatus.Submitted;
    }

    /// <summary>
    /// Draft/Submitted -&gt; Cancelled: driven only by the batch cancel (spec HR-05).
    /// </summary>
    /// <exception cref="HrValidationException">The slip is already Cancelled (<c>invalid_status_transition</c>).</exception>
    public void Cancel()
    {
        if (Status == SalarySlipStatus.Cancelled)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.InvalidStatusTransition,
                $"Salary slip '{SlipNumber}' is already Cancelled.");
        }

        Status = SalarySlipStatus.Cancelled;
    }
}
