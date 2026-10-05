using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Lists one company's salary components, ordered by name. Read-only.</summary>
public sealed class GetSalaryComponentsQueryHandler : IQueryHandler<GetSalaryComponentsQuery, IReadOnlyList<SalaryComponentDto>>
{
    private readonly IHrPayrollRepository _hr;

    public GetSalaryComponentsQueryHandler(IHrPayrollRepository hr)
    {
        _hr = hr;
    }

    public async Task<IReadOnlyList<SalaryComponentDto>> HandleAsync(
        GetSalaryComponentsQuery query,
        CancellationToken cancellationToken = default)
    {
        var components = await _hr.GetComponentsByCompanyAsync(query.CompanyId, cancellationToken);
        return components
            .OrderBy(c => c.ComponentName, StringComparer.Ordinal)
            .Select(SalaryComponentDto.Build)
            .ToList();
    }
}
