using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Items.Queries;

/// <summary>
/// Assembles <see cref="GetItemsQuery"/>: items from <see cref="IItemRepository"/>, balances from
/// <see cref="IStockRepository"/> (SUM of the Kardex rows per item/warehouse) and warehouse labels
/// from <see cref="IWarehouseRepository"/> - all tenant-isolated automatically (Constitution II.3).
/// </summary>
public sealed class GetItemsQueryHandler : IQueryHandler<GetItemsQuery, IReadOnlyList<ItemDto>>
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

    public async Task<IReadOnlyList<ItemDto>> HandleAsync(GetItemsQuery query, CancellationToken cancellationToken = default)
    {
        var items = await _items.GetAllAsync(cancellationToken);
        if (items.Count == 0)
        {
            return Array.Empty<ItemDto>();
        }

        var balances = await _stock.GetStockBalancesByCompanyAsync(query.CompanyId, cancellationToken);
        var warehouses = await _warehouses.GetByCompanyAsync(query.CompanyId, cancellationToken);

        var warehouseById = new Dictionary<Guid, Warehouse>(warehouses.Count);
        foreach (var warehouse in warehouses)
        {
            warehouseById[warehouse.Id] = warehouse;
        }

        var stockByItem = new Dictionary<Guid, List<ItemStockDto>>();
        foreach (var balance in balances)
        {
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
                warehouse.Code,
                warehouse.Name,
                balance.Qty,
                balance.Value));
        }

        var result = new List<ItemDto>(items.Count);
        foreach (var item in items)
        {
            var stock = stockByItem.TryGetValue(item.Id, out var list)
                ? (IReadOnlyList<ItemStockDto>)list
                : Array.Empty<ItemStockDto>();

            result.Add(ItemDto.From(item, stock));
        }

        return result;
    }
}
