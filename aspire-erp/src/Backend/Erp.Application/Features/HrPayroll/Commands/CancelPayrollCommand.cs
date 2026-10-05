using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.HrPayroll.Commands;

/// <summary>
/// Cancels one submitted payroll run (Task 12.4, spec HR-05): mirrors the accrual voucher,
/// marks the slips Cancelled and the entry Cancelled - atomically.
/// </summary>
/// <remarks>
/// BOUNDARY (documented): disbursed (Paid) runs are NEVER reversed here - the money already
/// left the bank, unwinding it is a banking-module concern. Paid -&gt; 409, Draft -&gt; 409.
/// <param name="RowVersion">Optional optimistic token - a stale token fails fast (banking Unreconcile precedent).</param>
/// </remarks>
public sealed record CancelPayrollCommand(
    Guid CompanyId,
    Guid PayrollEntryId,
    DateOnly? PostingDate = null,
    byte[]? RowVersion = null) : ICommand<Result<PayrollEntryDto>>;
