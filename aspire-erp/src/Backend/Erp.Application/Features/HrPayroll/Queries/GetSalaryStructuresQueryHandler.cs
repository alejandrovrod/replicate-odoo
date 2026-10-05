using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Lists one company's salary structures with their priced lines. Read-only.</summary>
public sealed class GetSalaryStructuresQueryHandler : IQueryHandler<GetSalaryStructuresQuery, IReadOnlyList<SalaryStructureDto>>
{
    private readonly IHrPayrollRepository _hr;

    public GetSalaryStructuresQueryHandler(IHrPayrollRepository hr)
    {
        _hr = hr;
    }

    public async Task<IReadOnlyList<SalaryStructureDto>> HandleAsync(
        GetSalaryStructuresQuery query,
        CancellationToken cancellationToken = default)
    {
        var structures = await _hr.GetStructuresByCompanyAsync(query.CompanyId, cancellationToken);
        var components = await _hr.GetComponentsByCompanyAsync(query.CompanyId, cancellationToken);
        var componentsById = components.ToDictionary(c => c.Id);
        return structures
            .OrderBy(s => s.StructureName, StringComparer.Ordinal)
            .Select(s => SalaryStructureDto.Build(s, componentsById))
            .ToList();
    }
}
