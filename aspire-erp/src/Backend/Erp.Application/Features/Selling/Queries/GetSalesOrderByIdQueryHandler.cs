using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>Single-order read of <see cref="GetSalesOrderByIdQuery"/> (null = 404 at the API).</summary>
public sealed class GetSalesOrderByIdQueryHandler
    : IQueryHandler<GetSalesOrderByIdQuery, SalesOrderDto?>
{
    private readonly ISalesOrderRepository _salesOrders;
    private readonly IItemRepository _items;

    public GetSalesOrderByIdQueryHandler(ISalesOrderRepository salesOrders, IItemRepository items)
    {
        _salesOrders = salesOrders;
        _items = items;
    }

    public async Task<SalesOrderDto?> HandleAsync(
        GetSalesOrderByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var order = await _salesOrders.GetOrderByIdAsync(query.SalesOrderId, cancellationToken);
        if (order is null || order.CompanyId != query.CompanyId)
        {
            return null;
        }

        var itemIds = new HashSet<Guid>();
        foreach (var line in order.Lines)
        {
            itemIds.Add(line.ItemId);
        }

        var items = await _items.GetByIdsAsync(new List<Guid>(itemIds), cancellationToken);
        var itemById = new Dictionary<Guid, Item>(items.Count);
        foreach (var item in items)
        {
            itemById[item.Id] = item;
        }

        return SalesOrderDto.Build(order, order.Customer, itemById);
    }
}
