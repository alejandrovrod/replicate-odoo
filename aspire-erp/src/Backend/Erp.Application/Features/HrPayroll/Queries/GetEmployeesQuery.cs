using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Lists one company's employees, ordered by employee number. Read-only.</summary>
public sealed record GetEmployeesQuery(Guid CompanyId) : IQuery<IReadOnlyList<EmployeeDto>>;
