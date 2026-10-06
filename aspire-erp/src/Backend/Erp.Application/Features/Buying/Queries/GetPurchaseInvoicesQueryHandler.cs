using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Buying.Queries;

/// <summary>
/// Assembles <see cref="GetPurchaseInvoicesQuery"/>: invoices from <see cref="IPurchaseRepository"/>,
/// line item codes/names from <see cref="IItemRepository"/>. Tenant isolation is automatic
/// (Constitution II.3); CompanyId is business scoping.
/// </summary>
public sealed class GetPurchaseInvoicesQueryHandler
    : IQueryHandler<GetPurchaseInvoicesQuery, PagedResult<PurchaseInvoiceDto>>
{
    private readonly IPurchaseRepository _purchases;
    private readonly IItemRepository _items;

    public GetPurchaseInvoicesQueryHandler(IPurchaseRepository purchases, IItemRepository items)
    {
        _purchases = purchases;
        _items = items;
    }

    public async Task<PagedResult<PurchaseInvoiceDto>> HandleAsync(
        GetPurchaseInvoicesQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _purchases.GetRecentInvoicesByCompanyAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        if (page.Items.Count == 0)
        {
            return page.Map(new List<PurchaseInvoiceDto>());
        }

        var itemIds = new HashSet<Guid>();
        foreach (var invoice in page.Items)
        {
            foreach (var line in invoice.Lines)
            {
                itemIds.Add(line.ItemId);
            }
        }

        var items = await _items.GetByIdsAsync(new List<Guid>(itemIds), cancellationToken);
        var itemById = new Dictionary<Guid, Item>(items.Count);
        foreach (var item in items)
        {
            itemById[item.Id] = item;
        }

        var result = new List<PurchaseInvoiceDto>(page.Items.Count);
        foreach (var invoice in page.Items)
        {
            result.Add(PurchaseInvoiceDto.Build(invoice, itemById));
        }

        return page.Map(result);
    }
}
