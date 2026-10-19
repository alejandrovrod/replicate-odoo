using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>
/// Reads one salary slip of a run as a printable pay stub (spec: formal receipt view):
/// earnings/deductions breakdown, settlement date, employee details and the company fiscal
/// header. Read-only. A missing/foreign run or slip resolves to null (404 at the API).
/// </summary>
public sealed record GetSalarySlipPrintQuery(
    Guid CompanyId,
    Guid PayrollEntryId,
    Guid SlipId) : IQuery<SalarySlipPrintableDto?>;
