using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Lists one company's employees, ordered by employee number. Read-only.</summary>
public sealed record GetEmployeesQuery(Guid CompanyId, int PageNumber = 1, int PageSize = 50) : IQuery<PagedResult<EmployeeDto>>;
