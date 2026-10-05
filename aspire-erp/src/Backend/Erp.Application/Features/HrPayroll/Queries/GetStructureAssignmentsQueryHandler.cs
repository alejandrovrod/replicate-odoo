using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Lists one company's structure assignments (the eligibility windows). Read-only.</summary>
public sealed class GetStructureAssignmentsQueryHandler : IQueryHandler<GetStructureAssignmentsQuery, IReadOnlyList<SalaryStructureAssignmentDto>>
{
    private readonly IHrPayrollRepository _hr;

    public GetStructureAssignmentsQueryHandler(IHrPayrollRepository hr)
    {
        _hr = hr;
    }

    public async Task<IReadOnlyList<SalaryStructureAssignmentDto>> HandleAsync(
        GetStructureAssignmentsQuery query,
        CancellationToken cancellationToken = default)
    {
        var assignments = await _hr.GetAssignmentsByCompanyAsync(query.CompanyId, cancellationToken);
        return assignments
            .OrderBy(a => a.EffectiveFrom)
            .Select(SalaryStructureAssignmentDto.Build)
            .ToList();
    }
}
