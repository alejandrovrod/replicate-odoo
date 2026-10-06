using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Lists one company's payroll batch headers, newest first. Read-only.</summary>
public sealed record GetPayrollEntriesQuery(Guid CompanyId, int PageNumber = 1, int PageSize = 50) : IQuery<PagedResult<PayrollEntryDto>>;
