using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Lists one company's salary components, ordered by name. Read-only.</summary>
public sealed class GetSalaryComponentsQueryHandler : IQueryHandler<GetSalaryComponentsQuery, PagedResult<SalaryComponentDto>>
{
    private readonly IHrPayrollRepository _hr;

    public GetSalaryComponentsQueryHandler(IHrPayrollRepository hr)
    {
        _hr = hr;
    }

    public async Task<PagedResult<SalaryComponentDto>> HandleAsync(
        GetSalaryComponentsQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _hr.GetComponentsByCompanyAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        return page.Map(page.Items.Select(SalaryComponentDto.Build).ToList());
    }
}
