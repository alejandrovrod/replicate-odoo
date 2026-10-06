using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Lists one company's employees, ordered by employee number. Read-only.</summary>
public sealed class GetEmployeesQueryHandler : IQueryHandler<GetEmployeesQuery, PagedResult<EmployeeDto>>
{
    private readonly IHrPayrollRepository _hr;

    public GetEmployeesQueryHandler(IHrPayrollRepository hr)
    {
        _hr = hr;
    }

    public async Task<PagedResult<EmployeeDto>> HandleAsync(
        GetEmployeesQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _hr.GetEmployeesByCompanyAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        return page.Map(page.Items.Select(EmployeeDto.Build).ToList());
    }
}
