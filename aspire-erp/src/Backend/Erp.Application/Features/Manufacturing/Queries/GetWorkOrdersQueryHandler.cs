using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Manufacturing.Queries;

/// <summary>Lists one company's work orders for the Task 9.5 execution board. Read-only.</summary>
public sealed class GetWorkOrdersQueryHandler : IQueryHandler<GetWorkOrdersQuery, IReadOnlyList<WorkOrderDto>>
{
    private readonly IManufacturingRepository _manufacturing;

    public GetWorkOrdersQueryHandler(IManufacturingRepository manufacturing)
    {
        _manufacturing = manufacturing;
    }

    public async Task<IReadOnlyList<WorkOrderDto>> HandleAsync(
        GetWorkOrdersQuery query,
        CancellationToken cancellationToken = default)
    {
        var orders = await _manufacturing.ListWorkOrdersAsync(query.CompanyId, cancellationToken);
        var result = new List<WorkOrderDto>(orders.Count);
        foreach (var order in orders)
        {
            result.Add(WorkOrderDto.Build(order));
        }

        return result;
    }
}
