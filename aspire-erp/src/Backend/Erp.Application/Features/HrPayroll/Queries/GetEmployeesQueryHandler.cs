using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Lists one company's employees, ordered by employee number. Read-only.</summary>
public sealed class GetEmployeesQueryHandler : IQueryHandler<GetEmployeesQuery, IReadOnlyList<EmployeeDto>>
{
    private readonly IHrPayrollRepository _hr;

    public GetEmployeesQueryHandler(IHrPayrollRepository hr)
    {
        _hr = hr;
    }

    public async Task<IReadOnlyList<EmployeeDto>> HandleAsync(
        GetEmployeesQuery query,
        CancellationToken cancellationToken = default)
    {
        var employees = await _hr.GetEmployeesByCompanyAsync(query.CompanyId, cancellationToken);
        return employees
            .OrderBy(e => e.EmployeeNumber, StringComparer.Ordinal)
            .Select(EmployeeDto.Build)
            .ToList();
    }
}
