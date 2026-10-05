using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.HrPayroll.Commands;

/// <summary>
/// Disburses one submitted payroll run (Task 12.4, spec HR-02 Phase 2 / HR-03): posts the
/// Dr 2150 PayrollPayable / Cr bank GL pair and marks the entry Paid - atomically.
/// </summary>
/// <remarks>
/// BOUNDARY (documented): only the GL pair is posted - no banking PaymentEntry voucher is
/// created (that module owns vouchers; payroll records the GL pair + Paid status).
/// <param name="RowVersion">Optional optimistic token - a stale token fails fast (banking Unreconcile precedent).</param>
/// </remarks>
public sealed record DisbursePayrollCommand(
    Guid CompanyId,
    Guid PayrollEntryId,
    Guid BankAccountId,
    DateOnly PostingDate,
    byte[]? RowVersion = null) : ICommand<Result<PayrollEntryDto>>;
