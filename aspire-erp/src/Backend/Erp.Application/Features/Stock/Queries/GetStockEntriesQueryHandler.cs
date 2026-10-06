using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Stock.Queries;

/// <summary>
/// Assembles <see cref="GetStockEntriesQuery"/>: vouchers from <see cref="IStockRepository"/>,
/// line item codes/names from <see cref="IItemRepository"/>. Tenant isolation is automatic
/// (Constitution II.3); CompanyId is business scoping.
/// </summary>
public sealed class GetStockEntriesQueryHandler : IQueryHandler<GetStockEntriesQuery, PagedResult<StockEntryDto>>
{
    private readonly IStockRepository _stock;
    private readonly IItemRepository _items;

    public GetStockEntriesQueryHandler(IStockRepository stock, IItemRepository items)
    {
        _stock = stock;
        _items = items;
    }

    public async Task<PagedResult<StockEntryDto>> HandleAsync(
        GetStockEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _stock.GetRecentByCompanyAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        if (page.Items.Count == 0)
        {
            return page.Map(new List<StockEntryDto>());
        }

        var itemIds = new HashSet<Guid>();
        foreach (var entry in page.Items)
        {
            foreach (var line in entry.Items)
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

        var result = new List<StockEntryDto>(page.Items.Count);
        foreach (var entry in page.Items)
        {
            var lines = new List<StockEntryLineDto>(entry.Items.Count);
            foreach (var line in entry.Items.OrderBy(l => l.LineNumber))
            {
                itemById.TryGetValue(line.ItemId, out var item);
                lines.Add(new StockEntryLineDto(
                    line.ItemId,
                    item?.ItemCode ?? line.ItemId.ToString(),
                    item?.ItemName ?? string.Empty,
                    line.Qty,
                    line.Rate,
                    line.LineNumber));
            }

            result.Add(new StockEntryDto(
                entry.Id,
                entry.CompanyId,
                entry.EntryType,
                entry.PostingDate,
                entry.VoucherNo,
                entry.WarehouseId,
                entry.TargetWarehouseId,
                entry.CreatedAt,
                lines));
        }

        return page.Map(result);
    }
}
