using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>
/// Assembles <see cref="GetSalesOrdersQuery"/>: orders (with lines and the customer identity that
/// rides on the aggregate) from <see cref="ISalesOrderRepository"/>, line item codes/names from
/// <see cref="IItemRepository"/>. Tenant isolation is automatic (Constitution II.3).
/// </summary>
public sealed class GetSalesOrdersQueryHandler
    : IQueryHandler<GetSalesOrdersQuery, PagedResult<SalesOrderDto>>
{
    private readonly ISalesOrderRepository _salesOrders;
    private readonly IItemRepository _items;

    public GetSalesOrdersQueryHandler(ISalesOrderRepository salesOrders, IItemRepository items)
    {
        _salesOrders = salesOrders;
        _items = items;
    }

    public async Task<PagedResult<SalesOrderDto>> HandleAsync(
        GetSalesOrdersQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _salesOrders.GetRecentOrdersByCompanyAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        if (page.Items.Count == 0)
        {
            return page.Map(new List<SalesOrderDto>());
        }

        var itemById = await LoadItemsAsync(page.Items, cancellationToken);

        var result = new List<SalesOrderDto>(page.Items.Count);
        foreach (var order in page.Items)
        {
            result.Add(SalesOrderDto.Build(order, order.Customer, itemById));
        }

        return page.Map(result);
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
