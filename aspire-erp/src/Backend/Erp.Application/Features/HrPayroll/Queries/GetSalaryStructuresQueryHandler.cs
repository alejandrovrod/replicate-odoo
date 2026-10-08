using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Lists one company's salary structures with their priced lines. Read-only.</summary>
public sealed class GetSalaryStructuresQueryHandler : IQueryHandler<GetSalaryStructuresQuery, PagedResult<SalaryStructureDto>>
{
    private readonly IHrPayrollRepository _hr;

    public GetSalaryStructuresQueryHandler(IHrPayrollRepository hr)
    {
        _hr = hr;
    }

    public async Task<PagedResult<SalaryStructureDto>> HandleAsync(
        GetSalaryStructuresQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _hr.GetStructuresByCompanyAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);

        // Components resolve per page (bounded lookup), never as a full-catalog read.
        var componentIds = new HashSet<Guid>();
        foreach (var structure in page.Items)
        {
            foreach (var line in structure.Lines)
            {
                componentIds.Add(line.ComponentId);
            }
        }

        var components = await _hr.GetComponentsByIdsAsync(new List<Guid>(componentIds), cancellationToken);
        var componentsById = new Dictionary<Guid, SalaryComponent>(components.Count);
        foreach (var component in components)
        {
            componentsById[component.Id] = component;
        }

        var result = new List<SalaryStructureDto>(page.Items.Count);
        foreach (var structure in page.Items)
        {
            result.Add(SalaryStructureDto.Build(structure, componentsById));
        }

        return page.Map(result);
    }
}
