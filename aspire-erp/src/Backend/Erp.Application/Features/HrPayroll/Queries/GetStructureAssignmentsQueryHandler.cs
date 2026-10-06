using Erp.Application.Common;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Lists one company's structure assignments (the eligibility windows). Read-only.</summary>
public sealed class GetStructureAssignmentsQueryHandler : IQueryHandler<GetStructureAssignmentsQuery, PagedResult<SalaryStructureAssignmentDto>>
{
    private readonly IHrPayrollRepository _hr;

    public GetStructureAssignmentsQueryHandler(IHrPayrollRepository hr)
    {
        _hr = hr;
    }

    public async Task<PagedResult<SalaryStructureAssignmentDto>> HandleAsync(
        GetStructureAssignmentsQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _hr.GetAssignmentsByCompanyAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        return page.Map(page.Items.Select(SalaryStructureAssignmentDto.Build).ToList());
    }
}
