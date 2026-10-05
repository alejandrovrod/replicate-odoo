using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.HrPayroll.Commands;

/// <summary>One attendance input of a submit run (no attendance/leave module - these arrive as run inputs).</summary>
public sealed record PaymentDayOverride(Guid EmployeeId, int PaymentDays, int AbsentDays);

/// <summary>
/// Runs one monthly payroll batch (Task 12.3, spec HR-01/HR-03/HR-04/HR-06): generates one
/// <see cref="SalarySlip"/> per eligible employee, posts the accrual voucher and marks the
/// entry Submitted - atomically, in ONE transaction.
/// </summary>
/// <remarks>
/// SINGLE-COMMAND verdict (documented design decision): slip generation AND the accrual
/// posting live in this one command (draft -&gt; Submitted with GL), mirroring the manufacture
/// precedent (one posting operation, draft -&gt; Completed with GL). A separate "generate then
/// accrue" step would leave posted-less Submitted batches and double the idempotency surface;
/// replay safety rides the endpoint <c>Idempotency-Key</c> (Article VI.4) plus the
/// skip-if-slip-exists guard and the HR-06 unique pair.
/// No RowVersion is carried: a create has no read-modify-write target to compare-and-swap
/// against (CAS lives on disburse/cancel, the banking Unreconcile precedent).
/// </remarks>
public sealed record SubmitPayrollRunCommand(
    Guid CompanyId,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly PostingDate,
    IReadOnlyList<PaymentDayOverride>? PaymentDayOverrides = null) : ICommand<Result<PayrollSubmitResultDto>>;
