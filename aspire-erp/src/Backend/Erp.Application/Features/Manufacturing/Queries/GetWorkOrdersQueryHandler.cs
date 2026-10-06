using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Manufacturing.Queries;

/// <summary>Lists one company's work orders for the Task 9.5 execution board. Read-only.</summary>
public sealed class GetWorkOrdersQueryHandler : IQueryHandler<GetWorkOrdersQuery, PagedResult<WorkOrderDto>>
{
    private readonly IManufacturingRepository _manufacturing;

    public GetWorkOrdersQueryHandler(IManufacturingRepository manufacturing)
    {
        _manufacturing = manufacturing;
    }

    public async Task<PagedResult<WorkOrderDto>> HandleAsync(
        GetWorkOrdersQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _manufacturing.ListWorkOrdersAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        var result = new List<WorkOrderDto>(page.Items.Count);
        foreach (var order in page.Items)
        {
            result.Add(WorkOrderDto.Build(order));
        }

        return page.Map(result);
    }
}
