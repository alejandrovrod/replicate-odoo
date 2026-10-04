using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>
/// Assembles <see cref="GetSalesOrdersQuery"/>: orders (with lines and the customer identity that
/// rides on the aggregate) from <see cref="ISalesOrderRepository"/>, line item codes/names from
/// <see cref="IItemRepository"/>. Tenant isolation is automatic (Constitution II.3).
/// </summary>
public sealed class GetSalesOrdersQueryHandler
    : IQueryHandler<GetSalesOrdersQuery, IReadOnlyList<SalesOrderDto>>
{
    private readonly ISalesOrderRepository _salesOrders;
    private readonly IItemRepository _items;

    public GetSalesOrdersQueryHandler(ISalesOrderRepository salesOrders, IItemRepository items)
    {
        _salesOrders = salesOrders;
        _items = items;
    }

    public async Task<IReadOnlyList<SalesOrderDto>> HandleAsync(
        GetSalesOrdersQuery query,
        CancellationToken cancellationToken = default)
    {
        var limit = query.Limit <= 0 ? 50 : Math.Min(query.Limit, 500);
        var orders = await _salesOrders.GetRecentOrdersByCompanyAsync(query.CompanyId, limit, cancellationToken);
        if (orders.Count == 0)
        {
            return Array.Empty<SalesOrderDto>();
        }

        var itemById = await LoadItemsAsync(orders, cancellationToken);

        var result = new List<SalesOrderDto>(orders.Count);
        foreach (var order in orders)
        {
            result.Add(SalesOrderDto.Build(order, order.Customer, itemById));
        }

        return result;
    }

    private async Task<Dictionary<Guid, Item>> LoadItemsAsync(
        IReadOnlyList<SalesOrder> orders,
        CancellationToken cancellationToken)
    {
        var itemIds = new HashSet<Guid>();
        foreach (var order in orders)
        {
            foreach (var line in order.Lines)
            {
                itemIds.Add(line.ItemId);
            }
        }

        var items = await _items.GetByIdsAsync(new List<Guid>(itemIds), cancellationToken);
        var byId = new Dictionary<Guid, Item>(items.Count);
        foreach (var item in items)
        {
            byId[item.Id] = item;
        }

        return byId;
    }
}
