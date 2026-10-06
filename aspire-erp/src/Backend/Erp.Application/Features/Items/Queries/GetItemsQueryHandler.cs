using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Items.Queries;

/// <summary>
/// Assembles <see cref="GetItemsQuery"/>: items from <see cref="IItemRepository"/>, balances from
/// <see cref="IStockRepository"/> (SUM of the Kardex rows per item/warehouse) and warehouse labels
/// from <see cref="IWarehouseRepository"/> - all tenant-isolated automatically (Constitution II.3).
/// </summary>
public sealed class GetItemsQueryHandler : IQueryHandler<GetItemsQuery, PagedResult<ItemDto>>
{
    private readonly IItemRepository _items;
    private readonly IStockRepository _stock;
    private readonly IWarehouseRepository _warehouses;

    public GetItemsQueryHandler(IItemRepository items, IStockRepository stock, IWarehouseRepository warehouses)
    {
        _items = items;
        _stock = stock;
        _warehouses = warehouses;
    }

    public async Task<PagedResult<ItemDto>> HandleAsync(GetItemsQuery query, CancellationToken cancellationToken = default)
    {
        var page = await _items.GetAllAsync(
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        if (page.Items.Count == 0)
        {
            return page.Map(new List<ItemDto>());
        }

        var balances = await _stock.GetStockBalancesByCompanyAsync(query.CompanyId, cancellationToken);
        var warehouses = await _warehouses.GetByCompanyAsync(query.CompanyId, cancellationToken);

        var warehouseById = new Dictionary<Guid, Warehouse>(warehouses.Count);
        foreach (var warehouse in warehouses)
        {
            warehouseById[warehouse.Id] = warehouse;
        }

        var pageIds = new HashSet<Guid>(page.Items.Count);
        foreach (var item in page.Items)
        {
            pageIds.Add(item.Id);
        }

        var stockByItem = new Dictionary<Guid, List<ItemStockDto>>();
        foreach (var balance in balances)
        {
            if (!pageIds.Contains(balance.ItemId))
            {
                continue;
            }

            if (!warehouseById.TryGetValue(balance.WarehouseId, out var warehouse))
            {
                continue;
            }

            if (!stockByItem.TryGetValue(balance.ItemId, out var list))
            {
                list = new List<ItemStockDto>();
                stockByItem[balance.ItemId] = list;
            }

            list.Add(new ItemStockDto(
                warehouse.Id,
                warehouse.WarehouseCode,
                warehouse.WarehouseName,
                balance.Qty,
                balance.Value));
        }

        var result = new List<ItemDto>(page.Items.Count);
        foreach (var item in page.Items)
        {
            var stock = stockByItem.TryGetValue(item.Id, out var list)
                ? (IReadOnlyList<ItemStockDto>)list
                : Array.Empty<ItemStockDto>();

            result.Add(ItemDto.From(item, stock));
        }

        return page.Map(result);
    }
}

