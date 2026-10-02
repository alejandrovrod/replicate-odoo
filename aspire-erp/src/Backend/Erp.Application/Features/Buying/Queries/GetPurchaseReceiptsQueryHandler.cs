using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Buying.Queries;

/// <summary>
/// Assembles <see cref="GetPurchaseReceiptsQuery"/>: receipts from <see cref="IPurchaseRepository"/>,
/// line item codes/names from <see cref="IItemRepository"/>. Tenant isolation is automatic
/// (Constitution II.3); CompanyId is business scoping.
/// </summary>
public sealed class GetPurchaseReceiptsQueryHandler
    : IQueryHandler<GetPurchaseReceiptsQuery, IReadOnlyList<PurchaseReceiptDto>>
{
    private readonly IPurchaseRepository _purchases;
    private readonly IItemRepository _items;

    public GetPurchaseReceiptsQueryHandler(IPurchaseRepository purchases, IItemRepository items)
    {
        _purchases = purchases;
        _items = items;
    }

    public async Task<IReadOnlyList<PurchaseReceiptDto>> HandleAsync(
        GetPurchaseReceiptsQuery query,
        CancellationToken cancellationToken = default)
    {
        var limit = query.Limit <= 0 ? 50 : Math.Min(query.Limit, 500);
        var receipts = await _purchases.GetRecentReceiptsByCompanyAsync(query.CompanyId, limit, cancellationToken);
        if (receipts.Count == 0)
        {
            return Array.Empty<PurchaseReceiptDto>();
        }

        var itemById = await LoadItemsAsync(receipts, cancellationToken);

        var result = new List<PurchaseReceiptDto>(receipts.Count);
        foreach (var receipt in receipts)
        {
            result.Add(PurchaseReceiptDto.Build(receipt, itemById));
        }

        return result;
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
