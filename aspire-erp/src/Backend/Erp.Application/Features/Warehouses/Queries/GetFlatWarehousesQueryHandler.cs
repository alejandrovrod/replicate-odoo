using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Warehouses.Queries;

/// <summary>Assembles <see cref="GetFlatWarehousesQuery"/> from <see cref="IWarehouseRepository"/>.</summary>
public sealed class GetFlatWarehousesQueryHandler : IQueryHandler<GetFlatWarehousesQuery, PagedResult<WarehouseDto>>
{
    private readonly IWarehouseRepository _warehouses;

    public GetFlatWarehousesQueryHandler(IWarehouseRepository warehouses)
    {
        _warehouses = warehouses;
    }

    public async Task<PagedResult<WarehouseDto>> HandleAsync(
        GetFlatWarehousesQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _warehouses.GetFlatWarehousesAsync(
            query.CompanyId,
            query.LeavesOnly,
            query.IsActive,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        return page.Map(page.Items.Select(WarehouseDto.From).ToList());
    }
}
