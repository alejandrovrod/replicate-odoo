using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Lists one company's payroll batch headers, newest first. Read-only.</summary>
public sealed record GetPayrollEntriesQuery(Guid CompanyId) : IQuery<IReadOnlyList<PayrollEntryDto>>;
