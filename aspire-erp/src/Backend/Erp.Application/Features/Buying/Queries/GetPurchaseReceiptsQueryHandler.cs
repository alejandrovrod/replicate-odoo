using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Buying.Queries;

/// <summary>
/// Assembles <see cref="GetPurchaseReceiptsQuery"/>: receipts from <see cref="IPurchaseRepository"/>,
/// line item codes/names from <see cref="IItemRepository"/>. Tenant isolation is automatic
/// (Constitution II.3); CompanyId is business scoping.
/// </summary>
public sealed class GetPurchaseReceiptsQueryHandler
    : IQueryHandler<GetPurchaseReceiptsQuery, PagedResult<PurchaseReceiptDto>>
{
    private readonly IPurchaseRepository _purchases;
    private readonly IItemRepository _items;

    public GetPurchaseReceiptsQueryHandler(IPurchaseRepository purchases, IItemRepository items)
    {
        _purchases = purchases;
        _items = items;
    }

    public async Task<PagedResult<PurchaseReceiptDto>> HandleAsync(
        GetPurchaseReceiptsQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _purchases.GetRecentReceiptsByCompanyAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        if (page.Items.Count == 0)
        {
            return page.Map(new List<PurchaseReceiptDto>());
        }

        var itemById = await LoadItemsAsync(page.Items, cancellationToken);

        var result = new List<PurchaseReceiptDto>(page.Items.Count);
        foreach (var receipt in page.Items)
        {
            result.Add(PurchaseReceiptDto.Build(receipt, itemById));
        }

        return page.Map(result);
    }

    private async Task<Dictionary<Guid, Item>> LoadItemsAsync(
        IReadOnlyList<PurchaseReceipt> receipts,
        CancellationToken cancellationToken)
    {
        var itemIds = new HashSet<Guid>();
        foreach (var receipt in receipts)
        {
            foreach (var line in receipt.Lines)
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
