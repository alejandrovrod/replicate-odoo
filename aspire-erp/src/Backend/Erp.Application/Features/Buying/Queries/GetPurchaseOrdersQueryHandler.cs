using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Buying.Queries;

/// <summary>
/// Assembles <see cref="GetPurchaseOrdersQuery"/>: orders from <see cref="IPurchaseRepository"/>,
/// supplier identity from <see cref="ISupplierRepository"/>, line item codes/names from
/// <see cref="IItemRepository"/>. Tenant isolation is automatic (Constitution II.3).
/// </summary>
public sealed class GetPurchaseOrdersQueryHandler
    : IQueryHandler<GetPurchaseOrdersQuery, PagedResult<PurchaseOrderDto>>
{
    private readonly IPurchaseRepository _purchases;
    private readonly ISupplierRepository _suppliers;
    private readonly IItemRepository _items;

    public GetPurchaseOrdersQueryHandler(
        IPurchaseRepository purchases,
        ISupplierRepository suppliers,
        IItemRepository items)
    {
        _purchases = purchases;
        _suppliers = suppliers;
        _items = items;
    }

    public async Task<PagedResult<PurchaseOrderDto>> HandleAsync(
        GetPurchaseOrdersQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _purchases.GetRecentOrdersByCompanyAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        if (page.Items.Count == 0)
        {
            return page.Map(new List<PurchaseOrderDto>());
        }

        var supplierIds = new List<Guid>(page.Items.Count);
        var itemIds = new HashSet<Guid>();
        foreach (var order in page.Items)
        {
            if (!supplierIds.Contains(order.SupplierId))
            {
                supplierIds.Add(order.SupplierId);
            }

            foreach (var line in order.Items)
            {
                itemIds.Add(line.ItemId);
            }
        }

        var suppliers = await _suppliers.GetByIdsAsync(supplierIds, cancellationToken);
        var supplierById = new Dictionary<Guid, Supplier>(suppliers.Count);
        foreach (var supplier in suppliers)
        {
            supplierById[supplier.Id] = supplier;
        }

        var items = await _items.GetByIdsAsync(new List<Guid>(itemIds), cancellationToken);
        var itemById = new Dictionary<Guid, Item>(items.Count);
        foreach (var item in items)
        {
            itemById[item.Id] = item;
        }

        var result = new List<PurchaseOrderDto>(page.Items.Count);
        foreach (var order in page.Items)
        {
            if (!supplierById.TryGetValue(order.SupplierId, out var supplier))
            {
                // Deleted suppliers cannot happen (FK Restrict); stay defensive like the item lookup.
                continue;
            }

            result.Add(PurchaseOrderDto.Build(order, supplier, itemById));
        }

        return page.Map(result);
    }
}
